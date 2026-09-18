using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ClarityGameOptimizer.Tests.Analysis
{
    /// <summary>Runs the real analyzer over the development project, and the texture reader over a texture imported for the test.</summary>
    public class BuildSizeAnalyzerTests
    {
        private const string Folder = "Assets/ClarityGameOptimizerBuildSizeTest";

        [TearDown]
        public void RemoveTemporaryAssets()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        [Test]
        public void Scans_both_scopes_of_the_development_project_into_valid_stamped_reports()
        {
            Report lastBuild = new BuildSizeAnalyzer().Scan(BuildSizeReport.LastBuildScope);
            Report project = new BuildSizeAnalyzer().Scan(BuildSizeReport.ProjectScope);

            foreach (Report report in new[] { lastBuild, project })
            {
                Assert.That(ReportValidator.Validate(report), Is.Empty);
                Assert.That(report.Area, Is.EqualTo(Area.BuildSize));
                Assert.That(report.PackageVersion, Is.Not.Empty);
                Assert.That(report.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(report.Metrics.Any(metric => metric.Key == "textures.count"), Is.True);
                Assert.That(report.Notes.Any(note => note.Contains(EditorUserBuildSettings.activeBuildTarget.ToString())), Is.True);
            }

            Assert.That(lastBuild.Scope, Is.EqualTo("Last build"));
            Assert.That(project.Scope, Is.EqualTo("Project"));
            Assert.That(project.Findings.Any(finding => finding.Check == BuildSizeReport.ReportMissingCheck), Is.False);
            if (BuildSizeAnalyzer.DescribeLastBuild() == null)
            {
                Assert.That(lastBuild.Findings.Single(finding => finding.Check == BuildSizeReport.ReportMissingCheck).Severity, Is.EqualTo(Severity.Advice), "the development project has never been built here");
            }
        }

        [Test]
        public void Reads_the_stored_format_back_and_diagnoses_an_odd_sprite()
        {
            string path = ImportPng("odd-sprite.png", 30, 45, TextureImporterCompression.Compressed);

            TextureDescription texture = BuildSizeAnalyzer.DescribeTexture(path, BuildSizeAnalyzer.PlatformSettingsName(EditorUserBuildSettings.activeBuildTarget));

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.Path, Is.EqualTo(path));
            Assert.That(texture.Width, Is.EqualTo(30));
            Assert.That(texture.Height, Is.EqualTo(45));
            Assert.That(texture.SourceWidth, Is.EqualTo(30));
            Assert.That(texture.SourceHeight, Is.EqualTo(45));
            Assert.That(texture.CompressionRequested, Is.True);
            Assert.That(texture.IsPng, Is.True);
            Assert.That(texture.MaxSize, Is.GreaterThan(0));
            Assert.That(TextureRules.IsRawFormat(texture.StoredFormat), Is.True, "30×45 defeats every block compressor, so Unity stored " + texture.StoredFormat);

            TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);
            Assert.That(diagnosis.Strategy, Is.EqualTo(UncompressedStrategy.PadSource));
            Assert.That(diagnosis.TargetWidth, Is.EqualTo(32));
            Assert.That(diagnosis.TargetHeight, Is.EqualTo(48));

            Report report = BuildSizeReport.Build(BuildSizeReport.ProjectScope, null, new[] { texture }, null, "Test");
            Assert.That(report.Findings.Single().Id, Is.EqualTo("texture.stored-uncompressed:" + path));
        }

        [Test]
        public void A_deliberately_uncompressed_texture_and_a_legal_one_are_not_fallbacks()
        {
            string deliberate = ImportPng("deliberate.png", 30, 45, TextureImporterCompression.Uncompressed);
            string legal = ImportPng("legal.png", 32, 32, TextureImporterCompression.Compressed);

            TextureDescription first = BuildSizeAnalyzer.DescribeTexture(deliberate, "Standalone");
            TextureDescription second = BuildSizeAnalyzer.DescribeTexture(legal, "Standalone");

            Assert.That(first.CompressionRequested, Is.False);
            Assert.That(TextureRules.Diagnose(first), Is.Null);
            Assert.That(TextureRules.IsRawFormat(second.StoredFormat), Is.False, second.StoredFormat);
            Assert.That(TextureRules.Diagnose(second), Is.Null);
            Assert.That(BuildSizeAnalyzer.DescribeTexture("Assets/DoesNotExist.png", "Standalone"), Is.Null);
        }

        [Test]
        public void Png_dimensions_come_from_the_header()
        {
            string path = ImportPng("header.png", 30, 45, TextureImporterCompression.Compressed);

            int width;
            int height;
            Assert.That(BuildSizeAnalyzer.TryReadPngSize(path, out width, out height), Is.True);
            Assert.That(width, Is.EqualTo(30));
            Assert.That(height, Is.EqualTo(45));
            Assert.That(BuildSizeAnalyzer.TryReadSourceSize(path, out width, out height), Is.True);
            Assert.That(width, Is.EqualTo(30));

            string text = Path.Combine(Path.GetTempPath(), "ClarityGameOptimizer-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(text, "not an image at all, and longer than a PNG header would be");
            try
            {
                Assert.That(BuildSizeAnalyzer.TryReadPngSize(text, out width, out height), Is.False);
            }
            finally
            {
                File.Delete(text);
            }
        }

        [Test]
        public void Packed_assets_map_to_paths_and_bytes_per_asset()
        {
            var build = new BuildDescription();
            build.PackedAssets.Add(new PackedAssetDescription("Assets/B.png", "Texture2D", 10));
            build.PackedAssets.Add(new PackedAssetDescription("Assets/B.png", "Sprite", 1));
            build.PackedAssets.Add(new PackedAssetDescription("Assets/A.png", "Texture2D", 5));
            build.PackedAssets.Add(new PackedAssetDescription("Assets/M.fbx", "Mesh", 7));
            build.PackedAssets.Add(new PackedAssetDescription("", "MonoBehaviour", 3));

            Dictionary<string, long> bytes = BuildSizeAnalyzer.BytesByAsset(build);
            Assert.That(bytes["Assets/B.png"], Is.EqualTo(11));
            Assert.That(bytes["Assets/A.png"], Is.EqualTo(5));
            Assert.That(bytes.ContainsKey(""), Is.False);
            Assert.That(BuildSizeAnalyzer.PathsOfType(build, "Texture2D"), Is.EqualTo(new[] { "Assets/A.png", "Assets/B.png" }));
            Assert.That(BuildSizeAnalyzer.PathsOfType(build, "Mesh"), Is.EqualTo(new[] { "Assets/M.fbx" }));
            Assert.That(BuildSizeAnalyzer.PathsOfType(build, "AudioClip"), Is.Empty);
        }

        [Test]
        public void Editor_enums_map_to_report_words()
        {
            Assert.That(BuildSizeAnalyzer.BackendName(ScriptingImplementation.Mono2x), Is.EqualTo("Mono"));
            Assert.That(BuildSizeAnalyzer.BackendName(ScriptingImplementation.IL2CPP), Is.EqualTo("IL2CPP"));
            Assert.That(BuildSizeAnalyzer.CompressionOf(ModelImporterMeshCompression.Off), Is.EqualTo(MeshCompressionLevel.Off));
            Assert.That(BuildSizeAnalyzer.CompressionOf(ModelImporterMeshCompression.Medium), Is.EqualTo(MeshCompressionLevel.Medium));
            Assert.That(BuildSizeAnalyzer.CompressionOf(ModelImporterMeshCompression.High), Is.EqualTo(MeshCompressionLevel.High));
            Assert.That(BuildSizeAnalyzer.PlatformSettingsName(BuildTarget.iOS), Is.EqualTo("iPhone"));
            Assert.That(BuildSizeAnalyzer.PlatformSettingsName(BuildTarget.StandaloneWindows64), Is.EqualTo("Standalone"));
            Assert.That(BuildSizeAnalyzer.PlatformSettingsName(BuildTarget.Android), Is.EqualTo("Android"));
            Assert.That(BuildSizeAnalyzer.PlatformSettingsName(BuildTarget.WebGL), Is.EqualTo("WebGL"));
            Assert.That(BuildSizeAnalyzer.BuiltAt(Path.GetTempPath(), new DateTime(2026, 1, 2)), Is.EqualTo(new DateTime(2026, 1, 2)), "no report file, so the fallback");
            Assert.That(BuildSizeAnalyzer.DescribeModel("Assets/DoesNotExist.fbx"), Is.Null);
        }

        private static string ImportPng(string name, int width, int height, TextureImporterCompression compression)
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets", Folder.Substring("Assets/".Length));
            }

            var source = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32((byte)(i % 251), (byte)(i % 241), (byte)(i % 239), (byte)(i % 2 == 0 ? 255 : 128));
            }

            source.SetPixels32(pixels);
            byte[] png = ImageConversion.EncodeToPNG(source);
            UnityEngine.Object.DestroyImmediate(source);

            string path = Folder + "/" + name;
            File.WriteAllBytes(Path.Combine(ReportFolders.ProjectRoot, path), png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.textureCompression = compression;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return path;
        }
    }
}
