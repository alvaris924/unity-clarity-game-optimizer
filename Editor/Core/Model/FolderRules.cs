using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Project-relative folder lists as settings hold them: how they are cleaned up and how a path is
    /// matched against them. Comparison ignores case and slash direction, since Windows does, and a folder
    /// matches itself and everything inside it but never a sibling that merely shares its prefix.
    /// </summary>
    internal static class FolderRules
    {
        /// <summary>Trims, turns backslashes into slashes, drops a leading "./" and trailing slashes, and removes blanks and duplicates, keeping the order.</summary>
        public static List<string> Normalize(IEnumerable<string> folders)
        {
            var result = new List<string>();
            if (folders == null)
            {
                return result;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in folders)
            {
                string folder = Clean(raw);
                if (folder.Length > 0 && seen.Add(folder))
                {
                    result.Add(folder);
                }
            }

            return result;
        }

        /// <summary>One folder per line, cleaned the same way.</summary>
        public static List<string> ParseLines(string text)
        {
            return Normalize((text ?? "").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
        }

        /// <summary>True when the path is the folder or lies inside it.</summary>
        public static bool IsUnder(string path, string folder)
        {
            string cleanPath = Clean(path);
            string cleanFolder = Clean(folder);
            if (cleanFolder.Length == 0 || cleanPath.Length < cleanFolder.Length)
            {
                return false;
            }

            if (!cleanPath.StartsWith(cleanFolder, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return cleanPath.Length == cleanFolder.Length || cleanPath[cleanFolder.Length] == '/';
        }

        public static bool IsUnderAny(string path, IEnumerable<string> folders)
        {
            if (folders == null)
            {
                return false;
            }

            foreach (string folder in folders)
            {
                if (IsUnder(path, folder))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Clean(string value)
        {
            string folder = (value ?? "").Trim().Replace('\\', '/');
            while (folder.StartsWith("./", StringComparison.Ordinal))
            {
                folder = folder.Substring(2);
            }

            return folder.TrimEnd('/');
        }
    }
}
