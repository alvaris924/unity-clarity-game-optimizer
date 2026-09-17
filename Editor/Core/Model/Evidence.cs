using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Where a finding or a node came from: a file in the project, an optional line range and a short label
    /// such as "asmdef" or "manifest". Paths are project-relative with forward slashes, the form the asset
    /// database uses, so an exporter can hand them to a viewer or the Editor can open them.
    /// </summary>
    internal sealed class Evidence
    {
        public Evidence(string path, int line = 0, int endLine = 0, string label = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Evidence needs a path.", nameof(path));
            }

            if (line < 0 || endLine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(line), "Lines are 1-based; 0 means the whole file.");
            }

            if (endLine != 0 && endLine < line)
            {
                throw new ArgumentOutOfRangeException(nameof(endLine), "The end line cannot come before the start line.");
            }

            Path = path;
            Line = line;
            EndLine = endLine;
            Label = label ?? "";
        }

        public string Path { get; }

        /// <summary>1-based; 0 when the whole file is meant.</summary>
        public int Line { get; }

        /// <summary>1-based and inclusive; 0 when a single line or the whole file is meant.</summary>
        public int EndLine { get; }

        public string Label { get; }

        /// <summary>"path", "path:line" or "path:line-endLine", the form code editors accept.</summary>
        public string Locator
        {
            get
            {
                if (Line == 0)
                {
                    return Path;
                }

                return EndLine == 0 ? Path + ":" + Line : Path + ":" + Line + "-" + EndLine;
            }
        }
    }
}
