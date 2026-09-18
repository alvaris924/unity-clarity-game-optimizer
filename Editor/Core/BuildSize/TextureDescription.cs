using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// A texture as Unity stored it for the active build target, next to what the importer asked for. The
    /// stored format is the one fact the Inspector never shows: the importer and the build report both name
    /// the format Unity wanted, and only the imported object says whether it fell back to raw pixels.
    /// </summary>
    internal sealed class TextureDescription
    {
        private string _storedFormat = "";

        public TextureDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A texture needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
        }

        public string Path { get; }

        /// <summary>Imported width, after Max Size clamped it.</summary>
        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>Dimensions of the file on disk; equal to the imported ones unless Max Size clamped them.</summary>
        public int SourceWidth { get; set; }

        public int SourceHeight { get; set; }

        /// <summary>The TextureFormat name of the imported object: ETC2_RGBA8, ASTC_6x6, DXT5, or a raw one such as RGBA32.</summary>
        public string StoredFormat
        {
            get { return _storedFormat; }
            set { _storedFormat = value ?? ""; }
        }

        /// <summary>False when the importer's compression setting is Uncompressed: a deliberate choice, never a fallback.</summary>
        public bool CompressionRequested { get; set; } = true;

        public bool HasMipmaps { get; set; }

        /// <summary>Whether the stored format carries alpha; decides between the 8 and the 4 bits per pixel block formats.</summary>
        public bool HasAlpha { get; set; }

        /// <summary>The Max Size in force for the active target: the platform override when there is one, else the default.</summary>
        public int MaxSize { get; set; }

        /// <summary>Only a PNG can be padded losslessly in place.</summary>
        public bool IsPng { get; set; }

        /// <summary>What the last build attributed to this texture, or -1 when the build did not say.</summary>
        public long ShippedBytes { get; set; } = -1;

        /// <summary>True when Max Size is downscaling: the source is larger than what imported.</summary>
        public bool IsClamped
        {
            get { return SourceWidth > Width || SourceHeight > Height; }
        }
    }
}
