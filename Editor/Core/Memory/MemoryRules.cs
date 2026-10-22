namespace ClarityGameOptimizer.Core
{
    /// <summary>The thresholds of the Memory report.</summary>
    internal sealed class MemoryRules
    {
        public static MemoryRules Default
        {
            get { return new MemoryRules(); }
        }

        /// <summary>How many textures the ranking lists.</summary>
        public int TopTextures { get; set; } = 15;

        /// <summary>How many meshes the ranking lists.</summary>
        public int TopMeshes { get; set; } = 10;

        /// <summary>A Read/Write texture at or above this many bytes is a Warning rather than Advice.</summary>
        public long ReadableTextureWarningBytes { get; set; } = 1024 * 1024;

        /// <summary>A Read/Write mesh with fewer vertices is not reported: the copy is too small to matter.</summary>
        public int ReadableMeshMinimumVertices { get; set; } = 2000;

        /// <summary>A clip that decompresses on load from this length up is reported.</summary>
        public double DecompressOnLoadSeconds { get; set; } = 10;
    }
}
