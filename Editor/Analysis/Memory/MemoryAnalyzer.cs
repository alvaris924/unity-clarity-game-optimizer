using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads what the build ships into memory: everything the enabled build scenes and the Resources folders reference,
    /// and for each texture, mesh, audio clip and TextMesh Pro font asset among it what the Editor's Profiler reports
    /// for the active build target, with the import settings that change it. A model contributes only the meshes
    /// something references, found by the object references in the scenes, prefabs and assets of the same set; a
    /// model under Resources, or one whose referrers are not text-serialized, contributes all of its meshes.
    /// Everything that decides what the report says lives in <see cref="MemoryReport"/>.
    /// </summary>
    internal sealed class MemoryAnalyzer
    {
        private const int UnloadEvery = 64;

        // A reference to an object inside another asset: {fileID: N, guid: G, type: 3}.
        private static readonly Regex ObjectReference = new Regex(@"\{fileID: (-?\d+), guid: ([0-9a-f]{32}), type: 3\}", RegexOptions.Compiled);

        public Report Scan(string scope)
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            string platform = BuildSizeAnalyzer.PlatformSettingsName(target);
            List<string> shipped = ShippedAssets();
            Dictionary<string, HashSet<long>> referenced = ReferencedObjects(shipped);

            var textures = new List<TextureMemoryDescription>();
            var meshes = new List<MeshMemoryDescription>();
            var audio = new List<AudioMemoryDescription>();
            var fonts = new List<FontMemoryDescription>();
            var progress = new ScanProgress();
            try
            {
                for (int i = 0; i < shipped.Count; i++)
                {
                    progress.Report("Measuring", i, shipped.Count);
                    string path = shipped[i];
                    AssetImporter importer = AssetImporter.GetAtPath(path);
                    if (importer is TextureImporter textureImporter)
                    {
                        TextureMemoryDescription texture = DescribeTexture(path, textureImporter, platform);
                        if (texture != null)
                        {
                            textures.Add(texture);
                        }
                    }
                    else if (importer is ModelImporter modelImporter)
                    {
                        meshes.AddRange(DescribeModel(path, modelImporter, referenced));
                    }
                    else if (importer is AudioImporter audioImporter)
                    {
                        AudioMemoryDescription clip = DescribeAudio(path, audioImporter, platform);
                        if (clip != null)
                        {
                            audio.Add(clip);
                        }
                    }
                    else if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    {
                        Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
                        if (type != null && type.Name == "TMP_FontAsset")
                        {
                            FontMemoryDescription font = DescribeFont(path);
                            if (font != null)
                            {
                                fonts.Add(font);
                            }
                        }
                        else if (type == typeof(Mesh))
                        {
                            meshes.AddRange(DescribeMeshAsset(path));
                        }
                    }

                    if (i % UnloadEvery == UnloadEvery - 1)
                    {
                        EditorUtility.UnloadUnusedAssetsImmediate();
                    }
                }
            }
            finally
            {
                progress.Clear();
                EditorUtility.UnloadUnusedAssetsImmediate();
            }

            Report report = MemoryReport.Build(MemoryReport.ShippedScope, textures, meshes, audio, fonts, target.ToString());
            ReportEnvironment.Stamp(report);
            return report;
        }

        /// <summary>The enabled build scenes, everything under a Resources folder, and all they reference, scripts left out.</summary>
        internal static List<string> ShippedAssets()
        {
            var roots = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    roots.Add(scene.path);
                }
            }

            foreach (string path in AssetDatabase.GetAllAssetPaths())
            {
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && IsUnderResources(path) && !IsUnderEditor(path) && !AssetDatabase.IsValidFolder(path))
                {
                    roots.Add(path);
                }
            }

            var shipped = new List<string>();
            foreach (string path in AssetDatabase.GetDependencies(roots.ToArray(), true))
            {
                if (IsUnderEditor(path) || path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                shipped.Add(path);
            }

            shipped.Sort(string.CompareOrdinal);
            return shipped;
        }

        /// <summary>For each asset GUID, the local file ids of the objects inside it that the text-serialized assets of the set point at.</summary>
        internal static Dictionary<string, HashSet<long>> ReferencedObjects(IReadOnlyList<string> paths)
        {
            var referenced = new Dictionary<string, HashSet<long>>(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) && !path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string text;
                try
                {
                    text = File.ReadAllText(Path.Combine(ReportFolders.ProjectRoot, path));
                }
                catch (IOException)
                {
                    continue;
                }

                if (!text.StartsWith("%YAML", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (Match match in ObjectReference.Matches(text))
                {
                    long id;
                    if (!long.TryParse(match.Groups[1].Value, out id))
                    {
                        continue;
                    }

                    HashSet<long> ids;
                    if (!referenced.TryGetValue(match.Groups[2].Value, out ids))
                    {
                        ids = new HashSet<long>();
                        referenced.Add(match.Groups[2].Value, ids);
                    }

                    ids.Add(id);
                }
            }

            return referenced;
        }

        internal static TextureMemoryDescription DescribeTexture(string path, TextureImporter importer, string platform)
        {
            var loaded = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (loaded == null)
            {
                return null;
            }

            TextureImporterPlatformSettings settings = platform.Length > 0 ? importer.GetPlatformTextureSettings(platform) : null;
            var texture = new TextureMemoryDescription(path)
            {
                Width = loaded.width,
                Height = loaded.height,
                StoredFormat = loaded is Texture2D texture2D ? texture2D.format.ToString() : loaded.graphicsFormat.ToString(),
                RuntimeBytes = Profiler.GetRuntimeMemorySizeLong(loaded),
                IsReadable = importer.isReadable,
                HasMipmaps = importer.mipmapEnabled,
                IsSprite = importer.textureType == TextureImporterType.Sprite,
                HasPlatformOverride = settings != null && settings.overridden,
                MaxSize = settings != null && settings.overridden ? settings.maxTextureSize : importer.maxTextureSize,
            };
            return texture;
        }

        internal static List<MeshMemoryDescription> DescribeModel(string path, ModelImporter importer, Dictionary<string, HashSet<long>> referenced)
        {
            var result = new List<MeshMemoryDescription>();
            string guid = AssetDatabase.AssetPathToGUID(path);
            HashSet<long> ids;
            referenced.TryGetValue(guid, out ids);
            bool everything = IsUnderResources(path) || ids == null || ids.Count == 0;
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                string meshGuid;
                long localId;
                if (!everything && (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mesh, out meshGuid, out localId) || !ids.Contains(localId)))
                {
                    continue;
                }

                result.Add(new MeshMemoryDescription(path, UniqueName(names, mesh.name))
                {
                    Vertices = mesh.vertexCount,
                    RuntimeBytes = Profiler.GetRuntimeMemorySizeLong(mesh),
                    IsReadable = importer.isReadable,
                });
            }

            return result;
        }

        internal static List<MeshMemoryDescription> DescribeMeshAsset(string path)
        {
            var result = new List<MeshMemoryDescription>();
            var names = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (Mesh mesh in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>())
            {
                result.Add(new MeshMemoryDescription(path, UniqueName(names, mesh.name))
                {
                    Vertices = mesh.vertexCount,
                    RuntimeBytes = Profiler.GetRuntimeMemorySizeLong(mesh),
                    IsReadable = mesh.isReadable,
                });
            }

            return result;
        }

        internal static AudioMemoryDescription DescribeAudio(string path, AudioImporter importer, string platform)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                return null;
            }

            AudioImporterSampleSettings settings = platform.Length > 0 && importer.ContainsSampleSettingsOverride(platform)
                ? importer.GetOverrideSampleSettings(platform)
                : importer.defaultSampleSettings;
            return new AudioMemoryDescription(path)
            {
                LengthSeconds = clip.length,
                Channels = clip.channels,
                Frequency = clip.frequency,
                LoadType = settings.loadType.ToString(),
                CompressionFormat = settings.compressionFormat.ToString(),
                RuntimeBytes = Profiler.GetRuntimeMemorySizeLong(clip),
            };
        }

        /// <summary>
        /// Reads the bake of a TextMesh Pro font asset through its serialized fields, so the package needs no TextMesh Pro
        /// reference; null when the fields are not where the supported TextMesh Pro versions keep them.
        /// </summary>
        internal static FontMemoryDescription DescribeFont(string path)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(path);
            if (asset == null)
            {
                return null;
            }

            var serialized = new SerializedObject(asset);
            SerializedProperty atlases = serialized.FindProperty("m_AtlasTextures");
            var font = new FontMemoryDescription(path)
            {
                SourceFontId = SourceFontId(serialized),
                FamilyName = ReadString(serialized, "m_FaceInfo.m_FamilyName"),
                PointSize = ReadNumber(serialized, "m_FaceInfo.m_PointSize"),
                Padding = (int)ReadNumber(serialized, "m_AtlasPadding"),
                AtlasWidth = (int)ReadNumber(serialized, "m_AtlasWidth"),
                AtlasHeight = (int)ReadNumber(serialized, "m_AtlasHeight"),
                IsDynamic = (int)ReadNumber(serialized, "m_AtlasPopulationMode") != 0,
            };
            if (atlases != null && atlases.isArray)
            {
                for (int i = 0; i < atlases.arraySize; i++)
                {
                    var atlas = atlases.GetArrayElementAtIndex(i).objectReferenceValue as Texture2D;
                    if (atlas == null)
                    {
                        continue;
                    }

                    font.AtlasCount++;
                    font.AtlasBytes += Profiler.GetRuntimeMemorySizeLong(atlas);
                    font.AtlasReadable |= atlas.isReadable;
                }
            }

            return font;
        }

        private static string SourceFontId(SerializedObject serialized)
        {
            string guid = ReadString(serialized, "m_SourceFontFileGUID");
            if (guid.Length > 0)
            {
                return guid;
            }

            SerializedProperty file = serialized.FindProperty("m_SourceFontFile");
            string fileGuid;
            long localId;
            if (file != null && file.objectReferenceValue != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(file.objectReferenceValue, out fileGuid, out localId))
            {
                return fileGuid;
            }

            return "";
        }

        private static string ReadString(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            return property != null && property.propertyType == SerializedPropertyType.String ? property.stringValue ?? "" : "";
        }

        private static float ReadNumber(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                return 0;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    return property.intValue;
                default:
                    return 0;
            }
        }

        private static string UniqueName(Dictionary<string, int> names, string name)
        {
            int seen;
            names.TryGetValue(name, out seen);
            names[name] = seen + 1;
            return seen == 0 ? name : name + " (" + (seen + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + ")";
        }

        private static bool IsUnderResources(string path)
        {
            return path.IndexOf("/Resources/", StringComparison.Ordinal) >= 0;
        }

        private static bool IsUnderEditor(string path)
        {
            return path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0 || path.StartsWith("Assets/Editor/", StringComparison.Ordinal);
        }
    }
}
