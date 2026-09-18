using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// What mesh compression costs and saves. The ratios were measured on one modular building pack (Medium
    /// took a 3,648 KB artifact to 1,172 KB, High to 905 KB) and are projections, not promises; the error is
    /// the worst-case position quantisation for the mesh's largest extent, which is what decides whether a
    /// modular kit still snaps.
    /// </summary>
    internal static class MeshRules
    {
        /// <summary>The share of the uncompressed size that remains at each level.</summary>
        public static float SizeRatio(MeshCompressionLevel level)
        {
            switch (level)
            {
                case MeshCompressionLevel.Low:
                    return 0.55f;
                case MeshCompressionLevel.Medium:
                    return 0.32f;
                case MeshCompressionLevel.High:
                    return 0.25f;
                default:
                    return 1f;
            }
        }

        /// <summary>
        /// Worst-case position error in millimetres for a mesh whose largest extent is the given size in metres.
        /// Medium and High were measured; Low was not and answers NaN.
        /// </summary>
        public static float ErrorMillimetres(MeshCompressionLevel level, float maxDimensionMetres)
        {
            float extent = Math.Max(0f, maxDimensionMetres);
            switch (level)
            {
                case MeshCompressionLevel.Low:
                    return float.NaN;
                case MeshCompressionLevel.Medium:
                    return extent / 2048f * 1000f;
                case MeshCompressionLevel.High:
                    return extent / 256f * 1000f;
                default:
                    return 0f;
            }
        }

        public static long ProjectedBytes(long bytes, MeshCompressionLevel level)
        {
            return (long)(Math.Max(0, bytes) * (double)SizeRatio(level));
        }
    }
}
