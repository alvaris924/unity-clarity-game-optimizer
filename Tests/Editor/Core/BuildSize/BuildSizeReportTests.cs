using System;
using System.Collections.Generic;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    /// <summary>The report builder over a build shaped like an Android app bundle, with the rows of every check.</summary>
    public class BuildSizeReportTests
    {
        private const string Staging = "E:/Project/Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/src/main";
        private const string Backup = "E:/Project/Library/Bee/Android/Prj/IL2CPP/Il2CppBackup";
        private const string Output = "C:/Builds/game.aab";
        private const long Mega = 1024 * 1024;

        private static BuildDescription AndroidBuild(bool detailed)
        {
            var build = new BuildDescription
            {
                Platform = "Android",
                OutputPath = Output,
                Result = "Succeeded",
                BuiltAt = new DateTime(2026, 9, 6, 13, 54, 0),
                TotalBytes = 5_000 * Mega,
                DetailedRequested = detailed,
                ScriptingBackend = "IL2CPP",
                StrippingLevel = "Low",
            };
            build.Files.Add(new BuildFileDescription(Output, "Android AppBundle", 214 * Mega));
            build.Files.Add(new BuildFileDescription("C:/Builds/game.symbols.zip", "Symbols", 700 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/arm64-v8a/libil2cpp.so", "so", 160 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/armeabi-v7a/libil2cpp.so", "so", 130 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/arm64-v8a/libunity.so", "so", 20 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/armeabi-v7a/libunity.so", "so", 18 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/armeabi-v7a/libvendor.so", "so", 3 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/jniLibs/armeabi-v7a/Desktop.dll", "dll", 2 * Mega));
            build.Files.Add(new BuildFileDescription("E:/Project/Library/Bee/Android/Prj/IL2CPP/Gradle/unityLibrary/symbols/arm64-v8a/libil2cpp.so", "so", 1400 * Mega));
            build.Files.Add(new BuildFileDescription(Backup + "/Managed/Assembly-CSharp.dll", "dll", 4 * Mega));
            build.Files.Add(new BuildFileDescription(Backup + "/Managed/Assembly-CSharp.pdb", "pdb", 1 * Mega));
            build.Files.Add(new BuildFileDescription(Backup + "/Managed/Vendor.dll", "dll", 1 * Mega));
            build.Files.Add(new BuildFileDescription(Backup + "/il2cppOutput/Assembly-CSharp.cpp", "cpp", 900 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/assets/bin/Data/data.unity3d", "unity3d", 100 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/assets/bin/Data/resources.resource", "resource", 40 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/assets/bin/Data/Resources/unity default resources", "unity default resources", 3 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/assets/aa/catalog.json", "json", 5 * Mega));
            build.Files.Add(new BuildFileDescription(Staging + "/assets/bin/Data/boot.config", "config", 100));
            if (detailed)
            {
                build.PackedAssets.Add(new PackedAssetDescription("Assets/Art/Buildings/Big.fbx", "Mesh", 30 * Mega));
                build.PackedAssets.Add(new PackedAssetDescription("Assets/Art/Buildings/Big.fbx", "Mesh", 10 * Mega));
                build.PackedAssets.Add(new PackedAssetDescription("Assets/Resources/Atlas.png", "Texture2D", 24 * Mega));
                build.PackedAssets.Add(new PackedAssetDescription("Assets/Resources/Atlas.png", "Sprite", 1024));
                build.PackedAssets.Add(new PackedAssetDescription("Assets/UI/Icon.png", "Texture2D", 8 * Mega));
                build.PackedAssets.Add(new PackedAssetDescription("Packages/com.unity.x/Runtime/Default.shader", "Shader", 2 * Mega));
                build.PackedAssets.Add(new PackedAssetDescription("", "MonoBehaviour", 6 * Mega));
            }

            return build;
        }

        private static List<TextureDescription> Textures()
        {
            return new List<TextureDescription>
            {
                new TextureDescription("Assets/Resources/Atlas.png") { Width = 2048, Height = 1366, SourceWidth = 2500, SourceHeight = 1667, StoredFormat = "RGBA32", HasAlpha = true, MaxSize = 2048, IsPng = true, ShippedBytes = 24 * Mega + 1024 },
                new TextureDescription("Assets/UI/Icon.png") { Width = 30, Height = 45, SourceWidth = 30, SourceHeight = 45, StoredFormat = "RGBA32", HasAlpha = true, IsPng = true, ShippedBytes = 8 * Mega },
                new TextureDescription("Assets/UI/Panel.jpg") { Width = 96, Height = 200, SourceWidth = 96, SourceHeight = 200, StoredFormat = "RGB24", HasAlpha = false },
                new TextureDescription("Assets/UI/Fine.png") { Width = 512, Height = 512, SourceWidth = 512, SourceHeight = 512, StoredFormat = "ETC2_RGBA8", HasAlpha = true },
                new TextureDescription("Assets/UI/Deliberate.png") { Width = 30, Height = 30, SourceWidth = 30, SourceHeight = 30, StoredFormat = "RGBA32", CompressionRequested = false },
                new TextureDescription("Assets/UI/Icon.png") { Width = 30, Height = 45, StoredFormat = "RGBA32" },
            };
        }

        private static List<ModelDescription> Models()
        {
            return new List<ModelDescription>
            {
                new ModelDescription("Assets/Art/Buildings/Big.fbx") { MeshCount = 3, VertexCount = 120000, MaxDimension = 28f, Compression = MeshCompressionLevel.Off, ArtifactBytes = 3 * Mega, ShippedBytes = 40 * Mega },
                new ModelDescription("Assets/Art/Props/Small.fbx") { MeshCount = 1, VertexCount = 200, MaxDimension = 0.5f, Compression = MeshCompressionLevel.Off, ArtifactBytes = 20 * 1024 },
                new ModelDescription("Assets/Art/Characters/Hero.fbx") { MeshCount = 1, VertexCount = 30000, MaxDimension = 2f, Compression = MeshCompressionLevel.Off, IsSkinned = true, ArtifactBytes = 5 * Mega },
                new ModelDescription("Assets/Art/Buildings/Done.fbx") { MeshCount = 2, VertexCount = 50000, MaxDimension = 20f, Compression = MeshCompressionLevel.Medium, ArtifactBytes = 2 * Mega },
                new ModelDescription("Assets/Art/Buildings/Big.fbx") { MeshCount = 3, Compression = MeshCompressionLevel.Off, ArtifactBytes = 3 * Mega },
            };
        }

        [Test]
        public void A_detailed_android_build_yields_a_valid_report_with_every_metric()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android");

            Assert.That(Validate(report), Is.Empty);
            Assert.That(report.Area, Is.EqualTo(Area.BuildSize));
            Assert.That(report.Scope, Is.EqualTo("Last build"));
            Assert.That(report.Graph.IsEmpty, Is.True);
            Assert.That(Metric(report, "build.shipped-bytes"), Is.EqualTo(214 * Mega), "the bundle alone; the symbols beside it do not ship");
            Assert.That(report.Metrics.Single(metric => metric.Key == "build.shipped-bytes").Budget, Is.EqualTo(Budget.Download));
            Assert.That(Metric(report, "build.total-bytes"), Is.EqualTo(5_000 * Mega));
            Assert.That(Metric(report, "build.files"), Is.EqualTo(18));
            Assert.That(Metric(report, "build.native-library-bytes"), Is.EqualTo((160 + 130 + 20 + 18 + 3 + 2) * Mega), "everything under an ABI folder, except the unstripped copy under symbols");
            Assert.That(Metric(report, "build.native-libraries"), Is.EqualTo(6));
            Assert.That(Metric(report, "build.abis"), Is.EqualTo(2));
            Assert.That(Metric(report, "build.managed-bytes"), Is.EqualTo(5 * Mega), "the .pdb is not an assembly");
            Assert.That(Metric(report, "build.managed-assemblies"), Is.EqualTo(2));
            Assert.That(report.Metrics.Single(metric => metric.Key == "build.managed-bytes").Label, Does.Contain("IL2CPP input"));
            Assert.That(Metric(report, "build.asset-data-bytes"), Is.EqualTo((100 + 40 + 3) * Mega));
            Assert.That(Metric(report, "resources.bytes"), Is.EqualTo(24 * Mega + 1024), "from the packed assets when the build has them");
            Assert.That(Metric(report, "streaming-assets.bytes"), Is.EqualTo(5 * Mega));
            Assert.That(Metric(report, "assets.packed"), Is.EqualTo(4));
            Assert.That(Metric(report, "assets.packed-bytes"), Is.EqualTo((30 + 10 + 24 + 8 + 2 + 6) * Mega + 1024));
            Assert.That(Metric(report, "textures.count"), Is.EqualTo(5), "the duplicate path counts once");
            Assert.That(Metric(report, "textures.stored-uncompressed"), Is.EqualTo(3));
            Assert.That(Metric(report, "textures.uncompressed-bytes"), Is.EqualTo(24 * Mega + 1024 + 8 * Mega + TextureRules.RawBytes(96, 200, false, false)));
            Assert.That(Metric(report, "textures.recoverable-bytes"), Is.EqualTo(report.Findings.Where(finding => finding.Check == BuildSizeReport.TextureUncompressedCheck).Sum(finding => finding.Measured.Value.Value - finding.Projected.Value.Value)), "the sum of the rows' savings");
            Assert.That(Metric(report, "textures.recoverable-bytes"), Is.InRange(20 * Mega, 22 * Mega));
            Assert.That(Metric(report, "models.count"), Is.EqualTo(4));
            Assert.That(Metric(report, "models.uncompressed"), Is.EqualTo(1), "small, skinned and already compressed models are left alone");
            Assert.That(Metric(report, "models.recoverable-bytes"), Is.EqualTo(40 * Mega - (long)(40 * Mega * (double)0.32f)));
            Assert.That(report.Metrics.Where(metric => metric.Value.Unit == Units.Bytes && metric.Key != "build.total-bytes").All(metric => metric.Budget == Budget.Download), Is.True, "every byte is download size");
        }

        [Test]
        public void The_output_audit_finds_the_odd_library_the_stray_file_and_the_stripping_level()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android");

            Finding outlier = report.Findings.Single(finding => finding.Check == BuildSizeReport.NativeOutlierCheck);
            Assert.That(outlier.Subject, Is.EqualTo("libvendor.so"));
            Assert.That(outlier.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(outlier.Title, Does.StartWith("Ships for armeabi-v7a but not for arm64-v8a"));
            Assert.That(outlier.Evidence.Single().Path, Is.EqualTo("armeabi-v7a/libvendor.so"));

            Finding stray = report.Findings.Single(finding => finding.Check == BuildSizeReport.UnexpectedFileCheck);
            Assert.That(stray.Subject, Is.EqualTo("armeabi-v7a/Desktop.dll"));
            Assert.That(stray.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(stray.Measured.Value.Value, Is.EqualTo(2 * Mega));

            Finding stripping = report.Findings.Single(finding => finding.Check == BuildSizeReport.StrippingLowCheck);
            Assert.That(stripping.Subject, Is.EqualTo("Android"));
            Assert.That(stripping.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(stripping.Title, Does.StartWith("Managed stripping is Low with IL2CPP"));
            Assert.That(stripping.Verdict, Does.Contain("Medium"));

            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.MonoBackendCheck), Is.False);
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.DebugSymbolsCheck), Is.False, "the symbols zip sits beside the bundle");
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.NotDetailedCheck), Is.False);
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.ReportMissingCheck), Is.False);
            Assert.That(report.Notes.Any(note => note.StartsWith("Native code per ABI: arm64-v8a 180 MB, armeabi-v7a 153 MB")), Is.True, string.Join("\n", report.Notes));
        }

        [Test]
        public void Packed_assets_and_folders_are_ranked_by_bytes()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android", new BuildSizeRules(topAssets: 3, topFolders: 2));

            List<Finding> assets = report.Findings.Where(finding => finding.Check == BuildSizeReport.TopAssetCheck).ToList();
            Assert.That(assets.Select(finding => finding.Subject), Is.EqualTo(new[] { "Assets/Art/Buildings/Big.fbx", "Assets/Resources/Atlas.png", "Assets/UI/Icon.png" }));
            Assert.That(assets[0].Title, Is.EqualTo("#1 of 4 packed assets · Mesh"));
            Assert.That(assets[1].Title, Is.EqualTo("#2 of 4 packed assets · 2 objects"), "a texture and its sprite");
            Assert.That(assets[0].Severity, Is.EqualTo(Severity.Info));
            Assert.That(assets[0].Budget, Is.EqualTo(Budget.Download));
            Assert.That(assets[0].Measured.Value.Value, Is.EqualTo(40 * Mega));
            Assert.That(assets[0].Evidence.Single().Label, Is.EqualTo("asset"));

            List<Finding> folders = report.Findings.Where(finding => finding.Check == BuildSizeReport.TopFolderCheck).ToList();
            Assert.That(folders.Select(finding => finding.Subject), Is.EqualTo(new[] { "Assets/Art", "Assets/Resources" }));
            Assert.That(folders[0].Title, Is.EqualTo("#1 of 4 folders · 1 asset"));
            Assert.That(folders[0].Measured.Value.Value, Is.EqualTo(40 * Mega));
        }

        [Test]
        public void Resources_share_is_flagged_when_it_dominates_the_payload()
        {
            Report heavy = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android");
            Finding finding = heavy.Findings.Single(f => f.Check == BuildSizeReport.ResourcesHeavyCheck);
            Assert.That(finding.Title, Does.StartWith("Resources folders hold 30% of the asset payload"));
            Assert.That(finding.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(finding.Measured.Value.Value, Is.EqualTo(24 * Mega + 1024));

            Report relaxed = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android", new BuildSizeRules(resourcesShareWarning: 0.5));
            Assert.That(relaxed.Findings.Any(f => f.Check == BuildSizeReport.ResourcesHeavyCheck), Is.False);

            Report plain = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(false), null, null, "Android");
            Finding fromFiles = plain.Findings.Single(f => f.Check == BuildSizeReport.ResourcesHeavyCheck);
            Assert.That(fromFiles.Title, Does.StartWith("Resources folders hold 28% of the asset payload"), "resources.resource against the serialized data when the build has no per-asset sizes");
        }

        [Test]
        public void Raw_textures_become_rows_with_the_fix_that_applies()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android");
            List<Finding> rows = report.Findings.Where(finding => finding.Check == BuildSizeReport.TextureUncompressedCheck).ToList();

            Assert.That(rows.Select(row => row.Subject), Is.EqualTo(new[] { "Assets/Resources/Atlas.png", "Assets/UI/Icon.png", "Assets/UI/Panel.jpg" }), "largest saving first");

            Finding atlas = rows[0];
            Assert.That(atlas.Severity, Is.EqualTo(Severity.Warning), "saves more than a megabyte");
            Assert.That(atlas.Budget, Is.EqualTo(Budget.Download));
            Assert.That(atlas.Title, Is.EqualTo("Stored as RGBA32 although compression was requested: 2048×1366 is not a multiple of 4, after Max Size 2048 clamped 2500×1667"));
            Assert.That(atlas.Verdict, Does.StartWith("Raise Max Size from 2048 to 4096 so the source, padded to 2500×1668, imports at its own size"));
            Assert.That(atlas.Measured.Value.Value, Is.EqualTo(24 * Mega + 1024), "what the build attributed to it");
            Assert.That(atlas.Projected.Value.Value, Is.LessThan(atlas.Measured.Value.Value));
            Assert.That(atlas.Evidence.Single().Path, Is.EqualTo("Assets/Resources/Atlas.png"));

            Finding icon = rows[1];
            Assert.That(icon.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(icon.Title, Is.EqualTo("Stored as RGBA32 although compression was requested: 30×45 is not a multiple of 4"));
            Assert.That(icon.Verdict, Is.EqualTo("Pad the source to 32×48 with up to 3 pixels of transparency"));
            Assert.That(icon.Projected.Value.Value, Is.EqualTo((long)(8 * Mega * ((double)(32 * 48) / (30 * 45 * 4)))), "the fix's ratio applied to the shipped bytes");

            Finding panel = rows[2];
            Assert.That(panel.Severity, Is.EqualTo(Severity.Advice), "small");
            Assert.That(panel.Title, Is.EqualTo("Stored as RGB24 although compression was requested: 96×200 is not a power of two"));
            Assert.That(panel.Verdict, Does.StartWith("Override the platform format"));
            Assert.That(panel.Measured.Value.Value, Is.EqualTo(TextureRules.RawBytes(96, 200, false, false)), "no build number, so the raw estimate");
            Assert.That(panel.Projected.Value.Value, Is.EqualTo(TextureRules.BlockBytes(96, 200, false, false)));

            var reexport = new TextureDescription("Assets/UI/Huge.tga") { Width = 256, Height = 171, SourceWidth = 4000, SourceHeight = 2667, StoredFormat = "RGBA32", HasAlpha = true, MaxSize = 256 };
            Finding manual = BuildSizeReport.Build(BuildSizeReport.ProjectScope, null, new[] { reexport }, null, "Android").Findings.Single(f => f.Check == BuildSizeReport.TextureUncompressedCheck);
            Assert.That(manual.Verdict, Does.StartWith("Re-export the source at a multiple of 4"));
            Assert.That(manual.Projected.HasValue, Is.False, "nothing automatic applies, so nothing is projected");

            var notPng = new TextureDescription("Assets/UI/Odd.jpg") { Width = 30, Height = 45, SourceWidth = 30, SourceHeight = 45, StoredFormat = "RGB24" };
            Finding jpg = BuildSizeReport.Build(BuildSizeReport.ProjectScope, null, new[] { notPng }, null, "Android").Findings.Single(f => f.Check == BuildSizeReport.TextureUncompressedCheck);
            Assert.That(jpg.Verdict, Does.EndWith("by re-exporting it since only a PNG pads in place"));
        }

        [Test]
        public void Large_uncompressed_meshes_become_rows_with_the_error_they_would_take()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(true), Textures(), Models(), "Android");
            Finding row = report.Findings.Single(finding => finding.Check == BuildSizeReport.MeshUncompressedCheck);

            Assert.That(row.Subject, Is.EqualTo("Assets/Art/Buildings/Big.fbx"));
            Assert.That(row.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(row.Budget, Is.EqualTo(Budget.Download));
            Assert.That(row.Title, Is.EqualTo("Mesh compression is off: 3 meshes, 120,000 vertices, largest extent 28 m"));
            Assert.That(row.Verdict, Does.StartWith("Set Mesh Compression to Medium: about a third of the size, with a worst-case position error near 13.7 mm at 28 m"));
            Assert.That(row.Measured.Value.Value, Is.EqualTo(40 * Mega), "the build's number wins over the artifact");
            Assert.That(row.Projected.Value.Value, Is.EqualTo((long)(40 * Mega * (double)0.32f)));
            Assert.That(row.Evidence.Single().Label, Is.EqualTo("model"));
        }

        [Test]
        public void Without_a_build_the_last_build_scope_says_so_and_the_project_scope_does_not()
        {
            Report last = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, null, Textures(), Models(), "StandaloneWindows64");
            Assert.That(Validate(last), Is.Empty);
            Finding missing = last.Findings.Single(finding => finding.Check == BuildSizeReport.ReportMissingCheck);
            Assert.That(missing.Subject, Is.EqualTo("Library/LastBuild.buildreport"));
            Assert.That(missing.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(missing.Verdict, Does.Contain("DetailedBuildReport"));
            Assert.That(last.Metrics.Any(metric => metric.Key == "build.shipped-bytes"), Is.False);
            Assert.That(last.Metrics.Any(metric => metric.Key == "textures.count"), Is.True);

            Report project = BuildSizeReport.Build(BuildSizeReport.ProjectScope, null, Textures(), Models(), "StandaloneWindows64");
            Assert.That(Validate(project), Is.Empty);
            Assert.That(project.Scope, Is.EqualTo("Project"));
            Assert.That(project.Findings.Any(finding => finding.Check == BuildSizeReport.ReportMissingCheck), Is.False);
            Assert.That(project.Notes.Any(note => note.StartsWith("Project scope reads every texture and model under Assets")), Is.True);
            Assert.That(project.Notes.Any(note => note.Contains("active build target, StandaloneWindows64.")), Is.True);
            Assert.That(project.Findings.Count(finding => finding.Check == BuildSizeReport.MeshUncompressedCheck), Is.EqualTo(1));
            Assert.That(project.Findings.Single(finding => finding.Check == BuildSizeReport.MeshUncompressedCheck).Measured.Value.Value, Is.EqualTo(40 * Mega));
        }

        [Test]
        public void A_build_without_per_asset_sizes_gets_the_advice_and_no_ranking()
        {
            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, AndroidBuild(false), Textures(), Models(), "iOS");

            Assert.That(Validate(report), Is.Empty);
            Finding advice = report.Findings.Single(finding => finding.Check == BuildSizeReport.NotDetailedCheck);
            Assert.That(advice.Title, Is.EqualTo("The Android build of 2026-09-06 recorded no per-asset sizes; it ran without the detailed report option"));
            Assert.That(advice.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(advice.Measured.Value.Value, Is.EqualTo(214 * Mega), "the shipped size, which the build cannot break down");
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.TopAssetCheck), Is.False);
            Assert.That(Metric(report, "assets.packed"), Is.EqualTo(0));
            Assert.That(Metric(report, "resources.bytes"), Is.EqualTo(40 * Mega), "from the resources file when the build has no per-asset sizes");
            Assert.That(report.Notes.Any(note => note.StartsWith("The build recorded no per-asset sizes")), Is.True);
            Assert.That(report.Notes.Any(note => note.Contains("active build target, iOS, which is not the platform of the last build.")), Is.True);

            var requested = AndroidBuild(false);
            requested.DetailedRequested = true;
            Assert.That(BuildSizeReport.Build(BuildSizeReport.LastBuildScope, requested, null, null, "").Findings.Single(f => f.Check == BuildSizeReport.NotDetailedCheck).Title, Does.EndWith("recorded no per-asset sizes"));
        }

        [Test]
        public void Mono_on_a_mobile_platform_and_shipped_symbols_are_advice()
        {
            var build = new BuildDescription { Platform = "iOS", OutputPath = "/Builds/ios", ScriptingBackend = "Mono", StrippingLevel = "Disabled" };
            build.Files.Add(new BuildFileDescription("/Builds/ios/Data/Managed/Assembly-CSharp.dll", "dll", 3 * Mega));
            build.Files.Add(new BuildFileDescription("/Builds/ios/Data/Managed/Assembly-CSharp.pdb", "pdb", 1 * Mega));
            build.Files.Add(new BuildFileDescription("/Builds/ios/Data/level0", "level0", 2 * Mega));

            Report report = BuildSizeReport.Build(BuildSizeReport.LastBuildScope, build, null, null, "iOS");

            Assert.That(Validate(report), Is.Empty);
            Finding mono = report.Findings.Single(finding => finding.Check == BuildSizeReport.MonoBackendCheck);
            Assert.That(mono.Measured.Value.Value, Is.EqualTo(3 * Mega));
            Assert.That(mono.Verdict, Does.Contain("IL2CPP"));
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.StrippingLowCheck), Is.False, "stripping is an IL2CPP lever");
            Finding symbols = report.Findings.Single(finding => finding.Check == BuildSizeReport.DebugSymbolsCheck);
            Assert.That(symbols.Subject, Is.EqualTo("Assembly-CSharp.pdb"));
            Assert.That(symbols.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(Metric(report, "build.shipped-bytes"), Is.EqualTo(6 * Mega));
            Assert.That(Metric(report, "build.abis"), Is.EqualTo(0));
            Assert.That(report.Findings.Any(finding => finding.Check == BuildSizeReport.NativeOutlierCheck), Is.False, "one ABI or none cannot have an outlier");
        }

        [Test]
        public void Rejects_an_unknown_scope_and_tolerates_missing_lists()
        {
            Assert.That(() => BuildSizeReport.Build("Everything", null, null, null, ""), Throws.ArgumentException);
            Report report = BuildSizeReport.Build(BuildSizeReport.ProjectScope, null, null, null, null);
            Assert.That(Validate(report), Is.Empty);
            Assert.That(Metric(report, "textures.count"), Is.EqualTo(0));
            Assert.That(Metric(report, "models.count"), Is.EqualTo(0));
            Assert.That(report.Findings, Is.Empty);
            Assert.That(BuildSizeReport.Scopes, Is.EqualTo(new[] { "Last build", "Project" }));

            Report projectWithBuild = BuildSizeReport.Build(BuildSizeReport.ProjectScope, AndroidBuild(true), Textures(), Models(), "Android");
            Assert.That(projectWithBuild.Metrics.Any(metric => metric.Key.StartsWith("build.")), Is.False, "the project scope reads project assets only");
            Assert.That(projectWithBuild.Findings.Any(finding => finding.Check.StartsWith("build.")), Is.False);
        }

        private static List<string> Validate(Report report)
        {
            report.CreatedAt = ReportFixtures.Timestamp;
            return ReportValidator.Validate(report);
        }

        private static double Metric(Report report, string key)
        {
            return report.Metrics.Single(metric => metric.Key == key).Value.Value;
        }
    }
}
