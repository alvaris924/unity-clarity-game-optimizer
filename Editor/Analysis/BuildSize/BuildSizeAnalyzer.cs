using System;
using System.Collections.Generic;
using System.IO;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Experimental;
using UnityEngine;
using UnityEngine.Rendering;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads what a build costs: the last build report on this machine (summary, files, packed assets), the
    /// textures as Unity stored them for the active target, and the models with their mesh compression. In
    /// the last-build scope the textures and models are the ones the build packed, when it recorded them;
    /// otherwise, and in the project scope, every one under Assets. Everything that decides what the report
    /// says lives in <see cref="BuildSizeReport"/>.
    /// </summary>
    internal sealed class BuildSizeAnalyzer
    {
        private const string ProgressTitle = "Clarity Game Optimizer";
        private const int UnloadEvery = 512;

        public Report Scan(string scope)
        {
            scope = scope ?? BuildSizeReport.LastBuildScope;
            BuildDescription build = scope == BuildSizeReport.LastBuildScope ? DescribeLastBuild() : null;
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;

            IReadOnlyList<TextureDescription> textures;
            IReadOnlyList<ModelDescription> models;
            if (build != null && build.HasPackedAssets)
            {
                Dictionary<string, long> bytesByAsset = BytesByAsset(build);
                textures = DescribeTextures(PathsOfType(build, "Texture2D"), bytesByAsset, target);
                models = DescribeModels(PathsOfType(build, "Mesh"), bytesByAsset);
            }
            else
            {
                textures = DescribeTextures(FindAssets("t:Texture2D"), null, target);
                models = DescribeModels(FindAssets("t:Model"), null);
            }

            Report report = BuildSizeReport.Build(scope, build, textures, models, target.ToString());
            ReportEnvironment.Stamp(report);
            return report;
        }

        /// <summary>The last build made on this machine, or null when there is none.</summary>
        public static BuildDescription DescribeLastBuild()
        {
            BuildReport report = BuildReport.GetLatestReport();
            return report == null ? null : Describe(report, ReportFolders.ProjectRoot);
        }

        internal static BuildDescription Describe(BuildReport report, string projectRoot)
        {
            BuildSummary summary = report.summary;
            NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(summary.platformGroup);
            var build = new BuildDescription
            {
                Platform = summary.platform.ToString(),
                OutputPath = summary.outputPath,
                Result = summary.result.ToString(),
                TotalBytes = (long)summary.totalSize,
                DetailedRequested = (summary.options & BuildOptions.DetailedBuildReport) != 0,
                BuiltAt = BuiltAt(projectRoot, summary.buildEndedAt),
                ScriptingBackend = BackendName(PlayerSettings.GetScriptingBackend(named)),
                StrippingLevel = PlayerSettings.GetManagedStrippingLevel(named).ToString(),
            };

            foreach (BuildFile file in report.GetFiles())
            {
                build.Files.Add(new BuildFileDescription(file.path, file.role, (long)file.size));
            }

            foreach (PackedAssets packed in report.packedAssets)
            {
                foreach (PackedAssetInfo entry in packed.contents)
                {
                    build.PackedAssets.Add(new PackedAssetDescription(entry.sourceAssetPath, entry.type != null ? entry.type.Name : "", (long)entry.packedSize));
                }
            }

            return build;
        }

        /// <summary>The report file's write time dates the build; the summary's own end time has been seen returning the present for a report written days earlier.</summary>
        internal static DateTime BuiltAt(string projectRoot, DateTime fallback)
        {
            try
            {
                string file = Path.Combine(projectRoot, BuildSizeReport.ReportPath);
                if (File.Exists(file))
                {
                    return File.GetLastWriteTime(file);
                }
            }
            catch (Exception)
            {
                // The date is context; an unreadable file must not fail the scan.
            }

            return fallback;
        }

        internal static string BackendName(ScriptingImplementation backend)
        {
            switch (backend)
            {
                case ScriptingImplementation.Mono2x:
                    return "Mono";
                case ScriptingImplementation.IL2CPP:
                    return "IL2CPP";
                default:
                    return backend.ToString();
            }
        }

        internal static Dictionary<string, long> BytesByAsset(BuildDescription build)
        {
            var bytes = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (PackedAssetDescription packed in build.PackedAssets)
            {
                if (packed.SourcePath.Length == 0)
                {
                    continue;
                }

                long total;
                bytes.TryGetValue(packed.SourcePath, out total);
                bytes[packed.SourcePath] = total + packed.Bytes;
            }

            return bytes;
        }

        internal static List<string> PathsOfType(BuildDescription build, string typeName)
        {
            var paths = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (PackedAssetDescription packed in build.PackedAssets)
            {
                if (packed.SourcePath.Length > 0 && packed.TypeName == typeName && seen.Add(packed.SourcePath))
                {
                    paths.Add(packed.SourcePath);
                }
            }

            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        private static List<string> FindAssets(string filter)
        {
            string[] guids = AssetDatabase.FindAssets(filter, new[] { "Assets" });
            var paths = new List<string>(guids.Length);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Length > 0 && seen.Add(path))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(string.CompareOrdinal);
            return paths;
        }

        internal static List<TextureDescription> DescribeTextures(IReadOnlyList<string> paths, Dictionary<string, long> bytesByAsset, BuildTarget target)
        {
            var textures = new List<TextureDescription>(paths.Count);
            string platform = PlatformSettingsName(target);
            var progress = new Progress();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    progress.Report("Reading textures", i, paths.Count);

                    TextureDescription texture = DescribeTexture(paths[i], platform);
                    if (texture == null)
                    {
                        continue;
                    }

                    long shipped;
                    if (bytesByAsset != null && bytesByAsset.TryGetValue(texture.Path, out shipped))
                    {
                        texture.ShippedBytes = shipped;
                    }

                    textures.Add(texture);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }

            return textures;
        }

        /// <summary>
        /// Null when the path is not a texture the importer owns, such as a render texture or a generated one.
        /// A texture whose importer says Uncompressed is described from the importer alone, without loading
        /// it: it can never be a fallback, and loading is what a scan of thousands of textures pays for.
        /// </summary>
        internal static TextureDescription DescribeTexture(string path, string platform)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return null;
            }

            TextureImporterPlatformSettings settings = platform.Length > 0 ? importer.GetPlatformTextureSettings(platform) : null;
            var texture = new TextureDescription(path)
            {
                CompressionRequested = importer.textureCompression != TextureImporterCompression.Uncompressed,
                HasMipmaps = importer.mipmapEnabled,
                MaxSize = settings != null && settings.overridden ? settings.maxTextureSize : importer.maxTextureSize,
                IsPng = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase),
            };

            if (!texture.CompressionRequested)
            {
                return texture;
            }

            var loaded = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (loaded == null || loaded.width <= 0 || loaded.height <= 0)
            {
                return null;
            }

            string format = loaded.format.ToString();
            texture.Width = loaded.width;
            texture.Height = loaded.height;
            texture.SourceWidth = loaded.width;
            texture.SourceHeight = loaded.height;
            texture.StoredFormat = format;
            texture.HasAlpha = format != "RGB24";
            Resources.UnloadAsset(loaded);

            if (TextureRules.IsRawFormat(format))
            {
                int width;
                int height;
                if (TryReadSourceSize(path, out width, out height))
                {
                    texture.SourceWidth = width;
                    texture.SourceHeight = height;
                }
            }

            return texture;
        }

        /// <summary>The name TextureImporter uses for a platform's override: Android, iPhone, Standalone, WebGL.</summary>
        internal static string PlatformSettingsName(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.iOS:
                    return "iPhone";
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64:
                case BuildTarget.StandaloneOSX:
                case BuildTarget.StandaloneLinux64:
                    return "Standalone";
                default:
                    return BuildPipeline.GetBuildTargetGroup(target).ToString();
            }
        }

        /// <summary>
        /// The dimensions of the file on disk, which Max Size may have clamped at import. A PNG answers from
        /// its header; anything else is decoded, which is why this runs only for the raw candidates.
        /// </summary>
        internal static bool TryReadSourceSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            try
            {
                if (TryReadPngSize(path, out width, out height))
                {
                    return true;
                }

                var probe = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    if (ImageConversion.LoadImage(probe, File.ReadAllBytes(path)) && probe.width > 0 && probe.height > 0)
                    {
                        width = probe.width;
                        height = probe.height;
                        return true;
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(probe);
                }
            }
            catch (Exception)
            {
                // An unreadable source counts as unclamped, the conservative reading.
            }

            return false;
        }

        /// <summary>Width and height straight out of a PNG header, without decoding.</summary>
        internal static bool TryReadPngSize(string path, out int width, out int height)
        {
            width = 0;
            height = 0;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var header = new byte[24];
                if (stream.Read(header, 0, 24) < 24 || header[0] != 0x89 || header[1] != 0x50 || header[2] != 0x4E || header[3] != 0x47)
                {
                    return false;
                }

                width = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
                height = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
                return width > 0 && height > 0;
            }
        }

        internal static List<ModelDescription> DescribeModels(IReadOnlyList<string> paths, Dictionary<string, long> bytesByAsset)
        {
            var models = new List<ModelDescription>(paths.Count);
            var progress = new Progress();
            try
            {
                for (int i = 0; i < paths.Count; i++)
                {
                    progress.Report("Reading models", i, paths.Count);

                    ModelDescription model = DescribeModel(paths[i]);
                    if (model == null)
                    {
                        continue;
                    }

                    long shipped;
                    if (bytesByAsset != null && bytesByAsset.TryGetValue(model.Path, out shipped))
                    {
                        model.ShippedBytes = shipped;
                    }

                    models.Add(model);
                    if (i % UnloadEvery == UnloadEvery - 1)
                    {
                        EditorUtility.UnloadUnusedAssetsImmediate();
                    }
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }

            return models;
        }

        /// <summary>A progress bar that redraws at most a few times a second; every redraw repaints the Editor, which over thousands of assets costs more than the reads.</summary>
        private sealed class Progress
        {
            private const double Interval = 0.25;
            private double _next;

            public void Report(string what, int index, int count)
            {
                double now = EditorApplication.timeSinceStartup;
                if (now < _next)
                {
                    return;
                }

                _next = now + Interval;
                EditorUtility.DisplayProgressBar(ProgressTitle, what + " " + (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " of " + count.ToString(System.Globalization.CultureInfo.InvariantCulture), (float)index / Math.Max(1, count));
            }
        }

        /// <summary>Null when the path is not a model file the model importer owns.</summary>
        internal static ModelDescription DescribeModel(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                return null;
            }

            var model = new ModelDescription(path)
            {
                Compression = CompressionOf(importer.meshCompression),
                ArtifactBytes = ArtifactBytes(path),
            };

            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                var mesh = asset as Mesh;
                if (mesh == null)
                {
                    continue;
                }

                model.MeshCount++;
                model.VertexCount += mesh.vertexCount;
                Vector3 size = mesh.bounds.size;
                model.MaxDimension = Mathf.Max(model.MaxDimension, Mathf.Max(size.x, Mathf.Max(size.y, size.z)));
                if (mesh.HasVertexAttribute(VertexAttribute.BlendWeight))
                {
                    model.IsSkinned = true;
                }

                Resources.UnloadAsset(mesh);
            }

            return model;
        }

        internal static MeshCompressionLevel CompressionOf(ModelImporterMeshCompression compression)
        {
            switch (compression)
            {
                case ModelImporterMeshCompression.Low:
                    return MeshCompressionLevel.Low;
                case ModelImporterMeshCompression.Medium:
                    return MeshCompressionLevel.Medium;
                case ModelImporterMeshCompression.High:
                    return MeshCompressionLevel.High;
                default:
                    return MeshCompressionLevel.Off;
            }
        }

        /// <summary>The size of the imported artifact in the Library, or -1 when the asset database cannot say.</summary>
        internal static long ArtifactBytes(string path)
        {
            try
            {
                ArtifactID id = AssetDatabaseExperimental.ProduceArtifact(new ArtifactKey(AssetDatabase.GUIDFromAssetPath(path)));
                string[] files;
                if (!AssetDatabaseExperimental.GetArtifactPaths(id, out files))
                {
                    return -1;
                }

                long total = 0;
                foreach (string file in files)
                {
                    if (File.Exists(file))
                    {
                        total += new FileInfo(file).Length;
                    }
                }

                return total;
            }
            catch (Exception)
            {
                return -1;
            }
        }
    }
}
