using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A texture the scope references, as the Editor holds it for the active build target.</summary>
    internal sealed class TextureMemoryDescription
    {
        private string _storedFormat = "";

        public TextureMemoryDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A texture needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
        }

        public string Path { get; }

        public int Width { get; set; }

        public int Height { get; set; }

        /// <summary>The TextureFormat name of the imported object, such as ASTC_6x6 or a raw RGBA32.</summary>
        public string StoredFormat
        {
            get { return _storedFormat; }
            set { _storedFormat = value ?? ""; }
        }

        /// <summary>What the Profiler reports for the loaded object; relative, not a device figure.</summary>
        public long RuntimeBytes { get; set; }

        /// <summary>Read/Write enabled: a CPU copy as large as the GPU one stays in memory.</summary>
        public bool IsReadable { get; set; }

        public bool HasMipmaps { get; set; }

        /// <summary>Imported as a Sprite (2D and UI): drawn near its own size, so mipmaps rarely help.</summary>
        public bool IsSprite { get; set; }

        /// <summary>The Max Size in force for the active target.</summary>
        public int MaxSize { get; set; }

        /// <summary>Whether the active target has a platform override, the only place a mobile Max Size or ASTC is set.</summary>
        public bool HasPlatformOverride { get; set; }
    }

    /// <summary>One mesh the scope references, which may be one of many inside a model file.</summary>
    internal sealed class MeshMemoryDescription
    {
        public MeshMemoryDescription(string path, string meshName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A mesh needs the path of its asset.", nameof(path));
            }

            Path = path.Replace('\\', '/');
            MeshName = meshName ?? "";
        }

        /// <summary>The model or mesh asset the mesh lives in.</summary>
        public string Path { get; }

        public string MeshName { get; }

        public int Vertices { get; set; }

        public long RuntimeBytes { get; set; }

        /// <summary>Read/Write enabled on the model: the vertex data is kept on the CPU too.</summary>
        public bool IsReadable { get; set; }

        /// <summary>The subject a finding uses: "path:mesh" when a model holds several meshes.</summary>
        public string Subject
        {
            get { return MeshName.Length == 0 ? Path : Path + ":" + MeshName; }
        }
    }

    /// <summary>An audio clip the scope references, with the load settings of the active target.</summary>
    internal sealed class AudioMemoryDescription
    {
        private string _loadType = "";
        private string _compressionFormat = "";

        public AudioMemoryDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("An audio clip needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
        }

        public string Path { get; }

        public double LengthSeconds { get; set; }

        public int Channels { get; set; }

        public int Frequency { get; set; }

        /// <summary>DecompressOnLoad, CompressedInMemory or Streaming.</summary>
        public string LoadType
        {
            get { return _loadType; }
            set { _loadType = value ?? ""; }
        }

        /// <summary>PCM, Vorbis, ADPCM, MP3 or AAC.</summary>
        public string CompressionFormat
        {
            get { return _compressionFormat; }
            set { _compressionFormat = value ?? ""; }
        }

        public long RuntimeBytes { get; set; }

        /// <summary>16-bit PCM of the whole clip: what Decompress On Load holds.</summary>
        public long DecodedBytes
        {
            get { return (long)(LengthSeconds * Math.Max(0, Frequency) * Math.Max(1, Channels) * 2); }
        }
    }

    /// <summary>A TextMesh Pro font asset the scope references, described by its bake.</summary>
    internal sealed class FontMemoryDescription
    {
        private string _sourceFontId = "";
        private string _familyName = "";

        public FontMemoryDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A font asset needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
        }

        public string Path { get; }

        /// <summary>The GUID of the font file it was baked from; empty when the asset does not say.</summary>
        public string SourceFontId
        {
            get { return _sourceFontId; }
            set { _sourceFontId = value ?? ""; }
        }

        public string FamilyName
        {
            get { return _familyName; }
            set { _familyName = value ?? ""; }
        }

        public float PointSize { get; set; }

        public int Padding { get; set; }

        public int AtlasWidth { get; set; }

        public int AtlasHeight { get; set; }

        public int AtlasCount { get; set; }

        /// <summary>Dynamic population adds glyphs at run time and needs a readable atlas; static never does.</summary>
        public bool IsDynamic { get; set; }

        public bool AtlasReadable { get; set; }

        /// <summary>The Profiler's figure for every atlas texture together.</summary>
        public long AtlasBytes { get; set; }

        /// <summary>Two assets with the same key bake the same glyphs into the same atlas layout.</summary>
        public string BakeKey
        {
            get
            {
                return SourceFontId + "|" + PointSize.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|" + Padding.ToString(System.Globalization.CultureInfo.InvariantCulture)
                       + "|" + AtlasWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + "x" + AtlasHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }
    }
}
