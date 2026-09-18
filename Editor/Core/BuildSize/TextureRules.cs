using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>Why the block compressor refused a texture.</summary>
    internal enum UncompressedReason
    {
        /// <summary>Width or height is not a multiple of 4; every block format but ASTC needs both to be.</summary>
        NotMultipleOfFour = 0,

        /// <summary>Both are multiples of 4 but not powers of two; the older formats such as ETC1 and PVRTC demand that.</summary>
        NotPowerOfTwo = 1,
    }

    /// <summary>What would make the texture compress again, in order of how little it touches.</summary>
    internal enum UncompressedStrategy
    {
        /// <summary>The dimensions are already legal for the newer block formats; a platform format override is enough.</summary>
        FormatOnly = 0,

        /// <summary>The source dimensions are the imported ones; padding the file by up to 3 pixels fixes the import.</summary>
        PadSource = 1,

        /// <summary>Max Size is downscaling to an illegal size; raising it so the padded source imports at its own size is smaller than the raw fallback.</summary>
        LiftClamp = 2,

        /// <summary>Max Size is downscaling, and lifting it would ship more than the raw fallback; the source needs a re-export.</summary>
        ManualReexport = 3,
    }

    /// <summary>The outcome of <see cref="TextureRules.Diagnose"/> for one texture Unity stored raw.</summary>
    internal sealed class TextureDiagnosis
    {
        public UncompressedReason Reason { get; set; }

        public UncompressedStrategy Strategy { get; set; }

        /// <summary>The dimensions the texture imports at once fixed.</summary>
        public int TargetWidth { get; set; }

        public int TargetHeight { get; set; }

        /// <summary>The Max Size that stops the clamp, or 0 when the strategy does not lift it.</summary>
        public int LiftMaxSizeTo { get; set; }

        /// <summary>The raw pixels as stored today, mipmaps included.</summary>
        public long CurrentBytes { get; set; }

        /// <summary>The block-compressed size after the fix; equal to the current size when nothing automatic applies.</summary>
        public long ProjectedBytes { get; set; }

        public long Saving
        {
            get { return Math.Max(0, CurrentBytes - ProjectedBytes); }
        }
    }

    /// <summary>
    /// The dimension rules of block compression, and the arithmetic of what a raw fallback costs. Block
    /// compressors need dimensions that are multiples of 4, the older ones powers of two, and when a texture
    /// breaks the rule Unity does not fail the import: it stores raw RGBA32 or RGB24, four to six times the
    /// size, and says nothing. Sprites are the usual victims because the sprite importer keeps the source
    /// dimensions as exported, and Max Size adds a trap of its own: its proportional downscale does not round
    /// to a multiple of 4, so a legal source can import at an illegal size.
    /// </summary>
    internal static class TextureRules
    {
        /// <summary>Unity's Max Size choices; a source larger than the last one needs a re-export.</summary>
        public static readonly int[] MaxSizeOptions = { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192, 16384 };

        private const float MipmapFactor = 4f / 3f;

        /// <summary>The raw formats a compressed import falls back to. Alpha8 is a legitimate single-channel choice and is not one of them.</summary>
        public static bool IsRawFormat(string format)
        {
            switch (format)
            {
                case "RGBA32":
                case "RGB24":
                case "ARGB32":
                case "BGRA32":
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsMultipleOfFour(int width, int height)
        {
            return width > 0 && height > 0 && width % 4 == 0 && height % 4 == 0;
        }

        public static bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        public static int NextMultipleOfFour(int value)
        {
            int remainder = value % 4;
            return remainder == 0 ? value : value + (4 - remainder);
        }

        /// <summary>The smallest Max Size option that fits the side, or 0 when none does.</summary>
        public static int SmallestMaxSizeAtLeast(int side)
        {
            foreach (int option in MaxSizeOptions)
            {
                if (option >= side)
                {
                    return option;
                }
            }

            return 0;
        }

        /// <summary>Bytes of a raw texture: 4 per pixel with alpha, 3 without, plus a third for mipmaps.</summary>
        public static long RawBytes(int width, int height, bool alpha, bool mipmaps)
        {
            return (long)(width * (long)height * (alpha ? 4f : 3f) * (mipmaps ? MipmapFactor : 1f));
        }

        /// <summary>Bytes of a block-compressed texture: 8 bits per pixel with alpha, 4 without, plus a third for mipmaps.</summary>
        public static long BlockBytes(int width, int height, bool alpha, bool mipmaps)
        {
            return (long)(width * (long)height * (alpha ? 1f : 0.5f) * (mipmaps ? MipmapFactor : 1f));
        }

        /// <summary>
        /// Null when the texture is not a silent fallback: compression was not requested, or the stored format is
        /// not raw. Otherwise why it fell back and what fixes it.
        /// </summary>
        public static TextureDiagnosis Diagnose(TextureDescription texture)
        {
            if (texture == null)
            {
                throw new ArgumentNullException(nameof(texture));
            }

            if (!texture.CompressionRequested || !IsRawFormat(texture.StoredFormat) || texture.Width <= 0 || texture.Height <= 0)
            {
                return null;
            }

            int width = texture.Width;
            int height = texture.Height;
            bool multipleOfFour = IsMultipleOfFour(width, height);
            bool alpha = texture.HasAlpha;
            bool mipmaps = texture.HasMipmaps;
            var diagnosis = new TextureDiagnosis
            {
                Reason = multipleOfFour ? UncompressedReason.NotPowerOfTwo : UncompressedReason.NotMultipleOfFour,
                CurrentBytes = RawBytes(width, height, alpha, mipmaps),
            };

            if (multipleOfFour)
            {
                diagnosis.Strategy = UncompressedStrategy.FormatOnly;
                diagnosis.TargetWidth = width;
                diagnosis.TargetHeight = height;
            }
            else if (!texture.IsClamped)
            {
                diagnosis.Strategy = UncompressedStrategy.PadSource;
                diagnosis.TargetWidth = NextMultipleOfFour(width);
                diagnosis.TargetHeight = NextMultipleOfFour(height);
            }
            else
            {
                int paddedWidth = NextMultipleOfFour(texture.SourceWidth);
                int paddedHeight = NextMultipleOfFour(texture.SourceHeight);
                int lift = SmallestMaxSizeAtLeast(Math.Max(paddedWidth, paddedHeight));
                long lifted = BlockBytes(paddedWidth, paddedHeight, alpha, mipmaps);
                if (lift > 0 && lifted < diagnosis.CurrentBytes)
                {
                    diagnosis.Strategy = UncompressedStrategy.LiftClamp;
                    diagnosis.LiftMaxSizeTo = lift;
                    diagnosis.TargetWidth = paddedWidth;
                    diagnosis.TargetHeight = paddedHeight;
                }
                else
                {
                    diagnosis.Strategy = UncompressedStrategy.ManualReexport;
                    diagnosis.TargetWidth = NextMultipleOfFour(width);
                    diagnosis.TargetHeight = NextMultipleOfFour(height);
                }
            }

            diagnosis.ProjectedBytes = diagnosis.Strategy == UncompressedStrategy.ManualReexport
                ? diagnosis.CurrentBytes
                : BlockBytes(diagnosis.TargetWidth, diagnosis.TargetHeight, alpha, mipmaps);
            return diagnosis;
        }
    }
}
