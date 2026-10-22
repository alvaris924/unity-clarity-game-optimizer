using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class MemoryReportTests
    {
        private const long Mega = 1024 * 1024;

        private static Report Build(TextureMemoryDescription[] textures = null, MeshMemoryDescription[] meshes = null, AudioMemoryDescription[] audio = null, FontMemoryDescription[] fonts = null, MemoryRules rules = null)
        {
            Report report = MemoryReport.Build(MemoryReport.ShippedScope, textures, meshes, audio, fonts, "Android", rules);
            report.CreatedAt = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(ReportValidator.Validate(report), Is.Empty);
            return report;
        }

        [Test]
        public void Totals_each_kind_in_runtime_ram()
        {
            Report report = Build(
                new[] { new TextureMemoryDescription("Assets/A.png") { RuntimeBytes = 3 * Mega } },
                new[] { new MeshMemoryDescription("Assets/M.fbx", "Body") { RuntimeBytes = 2 * Mega, Vertices = 100 } },
                new[] { new AudioMemoryDescription("Assets/S.wav") { RuntimeBytes = Mega } },
                new[] { new FontMemoryDescription("Assets/F.asset") { AtlasBytes = Mega } });

            Assert.That(report.Area, Is.EqualTo(Area.Memory));
            Assert.That(report.Metrics.Single(m => m.Key == "memory.total-bytes").Value.Value, Is.EqualTo(7 * Mega));
            Assert.That(report.Metrics.Where(m => m.Key.EndsWith("-bytes", StringComparison.Ordinal)).All(m => m.Budget == Budget.RuntimeRam), Is.True);
        }

        [Test]
        public void A_readable_texture_projects_half_its_memory()
        {
            Report report = Build(new[]
            {
                new TextureMemoryDescription("Assets/Big.png") { RuntimeBytes = 4 * Mega, IsReadable = true, Width = 1024, Height = 1024 },
                new TextureMemoryDescription("Assets/Small.png") { RuntimeBytes = 64 * 1024, IsReadable = true, Width = 128, Height = 128 },
            });

            Finding big = report.Findings.Single(f => f.Check == MemoryReport.TextureReadableCheck && f.Subject == "Assets/Big.png");
            Finding small = report.Findings.Single(f => f.Check == MemoryReport.TextureReadableCheck && f.Subject == "Assets/Small.png");
            Assert.That(big.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(big.Projected.Value.Value, Is.EqualTo(2 * Mega));
            Assert.That(small.Severity, Is.EqualTo(Severity.Advice));
        }

        [Test]
        public void A_non_power_of_two_texture_with_mipmaps_stored_raw_is_a_warning_with_the_cause()
        {
            Report report = Build(new[]
            {
                new TextureMemoryDescription("Assets/UI/Star.png") { Width = 228, Height = 224, StoredFormat = "RGBA32", HasMipmaps = true, IsSprite = true, RuntimeBytes = 266 * 1024 },
            });

            Finding finding = report.Findings.Single(f => f.Check == MemoryReport.MipmapsBlockCompressionCheck);
            Assert.That(finding.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(finding.Title, Does.Contain("not a power of two and has mipmaps"));
            Assert.That(finding.FixId, Is.EqualTo("texture.mipmaps-off"));
            Assert.That(finding.Projected.Value.Value, Is.LessThan(finding.Measured.Value.Value / 3));
            Assert.That(report.Findings.Any(f => f.Check == MemoryReport.SpriteMipmapsCheck), Is.False);
        }

        [Test]
        public void A_compressed_sprite_with_mipmaps_is_advice_to_drop_them()
        {
            Report report = Build(new[]
            {
                new TextureMemoryDescription("Assets/UI/Panel.png") { Width = 256, Height = 256, StoredFormat = "ASTC_4x4", HasMipmaps = true, IsSprite = true, RuntimeBytes = 4000 },
                new TextureMemoryDescription("Assets/World/Wall.png") { Width = 256, Height = 256, StoredFormat = "ASTC_6x6", HasMipmaps = true, IsSprite = false, RuntimeBytes = 4000 },
            });

            Finding finding = report.Findings.Single(f => f.Check == MemoryReport.SpriteMipmapsCheck);
            Assert.That(finding.Subject, Is.EqualTo("Assets/UI/Panel.png"));
            Assert.That(finding.Projected.Value.Value, Is.EqualTo(3000));
        }

        [Test]
        public void The_texture_ranking_is_largest_first_and_limited()
        {
            var textures = Enumerable.Range(1, 5).Select(i => new TextureMemoryDescription("Assets/T" + i + ".png") { RuntimeBytes = i * Mega, Width = 64, Height = 64, StoredFormat = "ASTC_6x6" }).ToArray();
            Report report = Build(textures, rules: new MemoryRules { TopTextures = 3 });

            string[] ranked = report.Findings.Where(f => f.Check == MemoryReport.TopTextureCheck).Select(f => f.Subject).ToArray();
            Assert.That(ranked, Is.EqualTo(new[] { "Assets/T5.png", "Assets/T4.png", "Assets/T3.png" }));
            Assert.That(report.Findings.First(f => f.Check == MemoryReport.TopTextureCheck).Severity, Is.EqualTo(Severity.Info));
        }

        [Test]
        public void Readable_meshes_are_reported_per_mesh_from_the_minimum_size()
        {
            Report report = Build(meshes: new[]
            {
                new MeshMemoryDescription("Assets/City.fbx", "Building") { Vertices = 5000, RuntimeBytes = Mega, IsReadable = true },
                new MeshMemoryDescription("Assets/City.fbx", "Sign") { Vertices = 40, RuntimeBytes = 4096, IsReadable = true },
                new MeshMemoryDescription("Assets/Hero.fbx", "") { Vertices = 9000, RuntimeBytes = 2 * Mega },
            });

            Assert.That(report.Findings.Where(f => f.Check == MemoryReport.MeshReadableCheck).Select(f => f.Subject), Is.EqualTo(new[] { "Assets/City.fbx:Building" }));
            Assert.That(report.Findings.Where(f => f.Check == MemoryReport.TopMeshCheck).Select(f => f.Subject).First(), Is.EqualTo("Assets/Hero.fbx"));
        }

        [Test]
        public void Long_clips_decompressed_on_load_are_warnings_and_music_should_stream()
        {
            Report report = Build(audio: new[]
            {
                new AudioMemoryDescription("Assets/Music.ogg") { LengthSeconds = 157, Channels = 2, Frequency = 44100, LoadType = "DecompressOnLoad" },
                new AudioMemoryDescription("Assets/Voice.ogg") { LengthSeconds = 12, Channels = 1, Frequency = 22050, LoadType = "DecompressOnLoad" },
                new AudioMemoryDescription("Assets/Shot.wav") { LengthSeconds = 0.4, Channels = 1, Frequency = 44100, LoadType = "DecompressOnLoad" },
                new AudioMemoryDescription("Assets/Loop.ogg") { LengthSeconds = 60, Channels = 2, Frequency = 44100, LoadType = "Streaming" },
            });

            Finding[] findings = report.Findings.Where(f => f.Check == MemoryReport.DecompressOnLoadCheck).ToArray();
            Assert.That(findings.Select(f => f.Subject), Is.EquivalentTo(new[] { "Assets/Music.ogg", "Assets/Voice.ogg" }));
            Finding music = findings.Single(f => f.Subject == "Assets/Music.ogg");
            Assert.That(music.Measured.Value.Value, Is.EqualTo(157L * 44100 * 2 * 2));
            Assert.That(music.Verdict, Does.StartWith("Stream it"));
            Assert.That(findings.Single(f => f.Subject == "Assets/Voice.ogg").Verdict, Does.StartWith("Load it Compressed In Memory"));
        }

        [Test]
        public void One_typeface_baked_twice_with_the_same_settings_is_a_duplicate_but_a_static_and_dynamic_pair_is_not()
        {
            Report report = Build(fonts: new[]
            {
                Font("Assets/Fonts/Sen_Blue.asset", "sen", false, 2 * Mega),
                Font("Assets/Fonts/Sen_Black.asset", "sen", false, 2 * Mega),
                Font("Assets/Fonts/Fredoka.asset", "fredoka", false, Mega),
                Font("Assets/Fonts/Fredoka Dynamic.asset", "fredoka", true, Mega),
            });

            Finding[] duplicates = report.Findings.Where(f => f.Check == MemoryReport.DuplicateFontCheck).ToArray();
            Assert.That(duplicates.Select(f => f.Subject), Is.EqualTo(new[] { "Assets/Fonts/Sen_Blue.asset" }));
            Assert.That(duplicates[0].Verdict, Does.Contain("Sen_Black.asset"));
            Assert.That(duplicates[0].Evidence.Select(e => e.Path), Contains.Item("Assets/Fonts/Sen_Black.asset"));
        }

        [Test]
        public void A_readable_static_atlas_is_advice_and_a_dynamic_one_is_not()
        {
            Report report = Build(fonts: new[]
            {
                Readable(Font("Assets/Fonts/Lato.asset", "lato", false, Mega)),
                Readable(Font("Assets/Fonts/Noto.asset", "noto", true, Mega)),
            });

            Assert.That(report.Findings.Where(f => f.Check == MemoryReport.ReadableStaticAtlasCheck).Select(f => f.Subject), Is.EqualTo(new[] { "Assets/Fonts/Lato.asset" }));
        }

        private static FontMemoryDescription Font(string path, string source, bool dynamic, long bytes)
        {
            return new FontMemoryDescription(path) { SourceFontId = source, PointSize = 87, Padding = 9, AtlasWidth = 1024, AtlasHeight = 1024, AtlasCount = 1, IsDynamic = dynamic, AtlasBytes = bytes };
        }

        private static FontMemoryDescription Readable(FontMemoryDescription font)
        {
            font.AtlasReadable = true;
            return font;
        }
    }
}
