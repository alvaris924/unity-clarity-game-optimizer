using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// One object the build packed, attributed to the asset it came from. One source asset can contribute
    /// several objects, so a texture with mipmaps or a model with many meshes appears more than once.
    /// </summary>
    internal sealed class PackedAssetDescription
    {
        public PackedAssetDescription(string sourcePath, string typeName, long bytes)
        {
            SourcePath = (sourcePath ?? "").Replace('\\', '/');
            TypeName = typeName ?? "";
            Bytes = Math.Max(0, bytes);
        }

        /// <summary>The asset path, or empty for scene and internal data.</summary>
        public string SourcePath { get; }

        /// <summary>The object type's short name: Texture2D, Mesh, AudioClip, MonoBehaviour.</summary>
        public string TypeName { get; }

        public long Bytes { get; }
    }
}
