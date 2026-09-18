using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>The importer's mesh compression setting, as Unity names the levels.</summary>
    internal enum MeshCompressionLevel
    {
        Off = 0,
        Low = 1,
        Medium = 2,
        High = 3,
    }

    /// <summary>
    /// A model file as imported: its meshes, the largest extent among them, the compression setting and
    /// what it weighs. Mesh compression is a download lever only, it decompresses at load.
    /// </summary>
    internal sealed class ModelDescription
    {
        public ModelDescription(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A model needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
        }

        public string Path { get; }

        public int MeshCount { get; set; }

        public int VertexCount { get; set; }

        /// <summary>The largest bounds axis across the file's meshes, in metres; the compression error scales with it.</summary>
        public float MaxDimension { get; set; }

        public MeshCompressionLevel Compression { get; set; }

        /// <summary>A mesh with blend weights; compression quantises skinning too, so these are left to a person.</summary>
        public bool IsSkinned { get; set; }

        /// <summary>The imported artifact's size in the Library, a good proxy for what the mesh data costs in a build.</summary>
        public long ArtifactBytes { get; set; } = -1;

        /// <summary>What the last build attributed to this model's meshes, or -1 when the build did not say.</summary>
        public long ShippedBytes { get; set; } = -1;

        /// <summary>The best available size: the build's number when there is one, else the artifact's.</summary>
        public long Bytes
        {
            get { return ShippedBytes >= 0 ? ShippedBytes : Math.Max(0, ArtifactBytes); }
        }
    }
}
