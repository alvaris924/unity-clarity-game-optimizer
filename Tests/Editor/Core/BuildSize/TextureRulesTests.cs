using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class TextureRulesTests
    {
        [Test]
        public void Raw_formats_are_the_four_fallbacks_and_not_alpha8()
        {
            Assert.That(TextureRules.IsRawFormat("RGBA32"), Is.True);
            Assert.That(TextureRules.IsRawFormat("RGB24"), Is.True);
            Assert.That(TextureRules.IsRawFormat("ARGB32"), Is.True);
            Assert.That(TextureRules.IsRawFormat("BGRA32"), Is.True);
            Assert.That(TextureRules.IsRawFormat("Alpha8"), Is.False);
            Assert.That(TextureRules.IsRawFormat("ETC2_RGBA8"), Is.False);
            Assert.That(TextureRules.IsRawFormat("ASTC_6x6"), Is.False);
            Assert.That(TextureRules.IsRawFormat(""), Is.False);
        }

        [Test]
        public void Dimension_arithmetic()
        {
            Assert.That(TextureRules.IsMultipleOfFour(32, 48), Is.True);
            Assert.That(TextureRules.IsMultipleOfFour(30, 48), Is.False);
            Assert.That(TextureRules.IsMultipleOfFour(0, 4), Is.False);
            Assert.That(TextureRules.IsPowerOfTwo(1024), Is.True);
            Assert.That(TextureRules.IsPowerOfTwo(48), Is.False);
            Assert.That(TextureRules.IsPowerOfTwo(0), Is.False);
            Assert.That(TextureRules.NextMultipleOfFour(29), Is.EqualTo(32));
            Assert.That(TextureRules.NextMultipleOfFour(32), Is.EqualTo(32));
            Assert.That(TextureRules.SmallestMaxSizeAtLeast(1000), Is.EqualTo(1024));
            Assert.That(TextureRules.SmallestMaxSizeAtLeast(1024), Is.EqualTo(1024));
            Assert.That(TextureRules.SmallestMaxSizeAtLeast(20000), Is.EqualTo(0));
            Assert.That(TextureRules.RawBytes(100, 100, true, false), Is.EqualTo(40000));
            Assert.That(TextureRules.RawBytes(100, 100, false, false), Is.EqualTo(30000));
            Assert.That(TextureRules.RawBytes(96, 96, true, true), Is.EqualTo((long)(96 * 96 * 4 * (4f / 3f))));
            Assert.That(TextureRules.BlockBytes(100, 100, true, false), Is.EqualTo(10000));
            Assert.That(TextureRules.BlockBytes(100, 100, false, false), Is.EqualTo(5000));
        }

        [Test]
        public void Only_a_raw_texture_that_asked_for_compression_is_a_fallback()
        {
            Assert.That(TextureRules.Diagnose(new TextureDescription("Assets/a.png") { Width = 30, Height = 30, SourceWidth = 30, SourceHeight = 30, StoredFormat = "ETC2_RGBA8" }), Is.Null);
            Assert.That(TextureRules.Diagnose(new TextureDescription("Assets/a.png") { Width = 30, Height = 30, SourceWidth = 30, SourceHeight = 30, StoredFormat = "RGBA32", CompressionRequested = false }), Is.Null);
            Assert.That(TextureRules.Diagnose(new TextureDescription("Assets/a.png") { Width = 0, Height = 30, StoredFormat = "RGBA32" }), Is.Null);
            Assert.That(() => TextureRules.Diagnose(null), Throws.ArgumentNullException);
        }

        [Test]
        public void Odd_dimensions_pad_when_the_source_is_what_imported()
        {
            var texture = new TextureDescription("Assets/UI/icon.png") { Width = 30, Height = 45, SourceWidth = 30, SourceHeight = 45, StoredFormat = "RGBA32", HasAlpha = true, IsPng = true };

            TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);

            Assert.That(diagnosis.Reason, Is.EqualTo(UncompressedReason.NotMultipleOfFour));
            Assert.That(diagnosis.Strategy, Is.EqualTo(UncompressedStrategy.PadSource));
            Assert.That(diagnosis.TargetWidth, Is.EqualTo(32));
            Assert.That(diagnosis.TargetHeight, Is.EqualTo(48));
            Assert.That(diagnosis.LiftMaxSizeTo, Is.EqualTo(0));
            Assert.That(diagnosis.CurrentBytes, Is.EqualTo(30 * 45 * 4));
            Assert.That(diagnosis.ProjectedBytes, Is.EqualTo(32 * 48));
            Assert.That(diagnosis.Saving, Is.EqualTo(30 * 45 * 4 - 32 * 48));
        }

        [Test]
        public void Legal_for_block_formats_but_not_a_power_of_two_needs_only_a_format_override()
        {
            var texture = new TextureDescription("Assets/UI/panel.png") { Width = 96, Height = 200, SourceWidth = 96, SourceHeight = 200, StoredFormat = "RGB24", HasAlpha = false, HasMipmaps = true };

            TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);

            Assert.That(diagnosis.Reason, Is.EqualTo(UncompressedReason.NotPowerOfTwo));
            Assert.That(diagnosis.Strategy, Is.EqualTo(UncompressedStrategy.FormatOnly));
            Assert.That(diagnosis.TargetWidth, Is.EqualTo(96));
            Assert.That(diagnosis.TargetHeight, Is.EqualTo(200));
            Assert.That(diagnosis.CurrentBytes, Is.EqualTo(TextureRules.RawBytes(96, 200, false, true)));
            Assert.That(diagnosis.ProjectedBytes, Is.EqualTo(TextureRules.BlockBytes(96, 200, false, true)));
        }

        [Test]
        public void A_clamped_source_lifts_max_size_when_that_ships_less()
        {
            var texture = new TextureDescription("Assets/UI/bg.png") { Width = 1024, Height = 683, SourceWidth = 1500, SourceHeight = 1000, StoredFormat = "RGBA32", HasAlpha = true, MaxSize = 1024 };

            TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);

            Assert.That(texture.IsClamped, Is.True);
            Assert.That(diagnosis.Reason, Is.EqualTo(UncompressedReason.NotMultipleOfFour));
            Assert.That(diagnosis.Strategy, Is.EqualTo(UncompressedStrategy.LiftClamp));
            Assert.That(diagnosis.LiftMaxSizeTo, Is.EqualTo(2048));
            Assert.That(diagnosis.TargetWidth, Is.EqualTo(1500));
            Assert.That(diagnosis.TargetHeight, Is.EqualTo(1000));
            Assert.That(diagnosis.ProjectedBytes, Is.EqualTo(1500 * 1000), "8 bits per pixel at the padded source size");
            Assert.That(diagnosis.ProjectedBytes, Is.LessThan(diagnosis.CurrentBytes));
        }

        [Test]
        public void A_clamped_source_that_would_ship_more_at_full_size_needs_a_re_export()
        {
            var texture = new TextureDescription("Assets/UI/huge.png") { Width = 256, Height = 171, SourceWidth = 4000, SourceHeight = 2667, StoredFormat = "RGBA32", HasAlpha = true, MaxSize = 256 };

            TextureDiagnosis diagnosis = TextureRules.Diagnose(texture);

            Assert.That(diagnosis.Strategy, Is.EqualTo(UncompressedStrategy.ManualReexport));
            Assert.That(diagnosis.LiftMaxSizeTo, Is.EqualTo(0));
            Assert.That(diagnosis.TargetWidth, Is.EqualTo(256));
            Assert.That(diagnosis.TargetHeight, Is.EqualTo(172));
            Assert.That(diagnosis.ProjectedBytes, Is.EqualTo(diagnosis.CurrentBytes), "nothing automatic applies");
            Assert.That(diagnosis.Saving, Is.EqualTo(0));

            var beyond = new TextureDescription("Assets/UI/giant.png") { Width = 30, Height = 30, SourceWidth = 20000, SourceHeight = 30, StoredFormat = "RGBA32", MaxSize = 32 };
            Assert.That(TextureRules.Diagnose(beyond).Strategy, Is.EqualTo(UncompressedStrategy.ManualReexport), "larger than the biggest Max Size");
        }

        [Test]
        public void Mesh_rules_project_size_and_error()
        {
            Assert.That(MeshRules.SizeRatio(MeshCompressionLevel.Off), Is.EqualTo(1f));
            Assert.That(MeshRules.SizeRatio(MeshCompressionLevel.Medium), Is.EqualTo(0.32f));
            Assert.That(MeshRules.SizeRatio(MeshCompressionLevel.High), Is.EqualTo(0.25f));
            Assert.That(MeshRules.ProjectedBytes(1000, MeshCompressionLevel.Medium), Is.EqualTo((long)(1000 * (double)0.32f)));
            Assert.That(MeshRules.ProjectedBytes(-5, MeshCompressionLevel.Medium), Is.EqualTo(0));
            Assert.That(MeshRules.ErrorMillimetres(MeshCompressionLevel.Medium, 28f), Is.EqualTo(28f / 2048f * 1000f).Within(0.001f));
            Assert.That(MeshRules.ErrorMillimetres(MeshCompressionLevel.High, 28f), Is.EqualTo(28f / 256f * 1000f).Within(0.001f));
            Assert.That(MeshRules.ErrorMillimetres(MeshCompressionLevel.Off, 28f), Is.EqualTo(0f));
            Assert.That(float.IsNaN(MeshRules.ErrorMillimetres(MeshCompressionLevel.Low, 28f)), Is.True, "not measured");
        }
    }
}
