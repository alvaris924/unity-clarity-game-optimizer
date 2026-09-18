using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>The thresholds of the Build Size checks: where a raw texture becomes a warning, how small a mesh is left alone, how many rows the rankings keep.</summary>
    internal sealed class BuildSizeRules
    {
        public const long DefaultUncompressedWarningBytes = 1024 * 1024;
        public const long DefaultMeshMinimumBytes = 256 * 1024;
        public const int DefaultTopAssets = 25;
        public const int DefaultTopFolders = 10;
        public const double DefaultResourcesShareWarning = 0.25;

        public BuildSizeRules(long uncompressedWarningBytes = DefaultUncompressedWarningBytes, long meshMinimumBytes = DefaultMeshMinimumBytes, int topAssets = DefaultTopAssets, int topFolders = DefaultTopFolders, double resourcesShareWarning = DefaultResourcesShareWarning)
        {
            UncompressedWarningBytes = Math.Max(0, uncompressedWarningBytes);
            MeshMinimumBytes = Math.Max(0, meshMinimumBytes);
            TopAssets = Math.Max(0, topAssets);
            TopFolders = Math.Max(0, topFolders);
            ResourcesShareWarning = Math.Min(1.0, Math.Max(0.0, resourcesShareWarning));
        }

        public static BuildSizeRules Default
        {
            get { return new BuildSizeRules(); }
        }

        /// <summary>A raw texture whose fix saves at least this much is a warning; below it, advice.</summary>
        public long UncompressedWarningBytes { get; }

        /// <summary>Models below this size are not worth a compression row.</summary>
        public long MeshMinimumBytes { get; }

        public int TopAssets { get; }

        public int TopFolders { get; }

        /// <summary>The share of the asset payload under Resources folders above which the report says so.</summary>
        public double ResourcesShareWarning { get; }
    }
}
