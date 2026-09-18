using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>One file the build report lists: the path with forward slashes, the role Unity gave it (mostly the extension) and its size.</summary>
    internal sealed class BuildFileDescription
    {
        public BuildFileDescription(string path, string role, long bytes)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("A build file needs a path.", nameof(path));
            }

            Path = path.Replace('\\', '/');
            Role = role ?? "";
            Bytes = Math.Max(0, bytes);
        }

        public string Path { get; }

        public string Role { get; }

        public long Bytes { get; }

        public string FileName
        {
            get
            {
                int slash = Path.LastIndexOf('/');
                return slash < 0 ? Path : Path.Substring(slash + 1);
            }
        }
    }
}
