using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Finds an archify checkout on this machine. archify is an external tool, never a dependency: it is
    /// used when found and the diagram files stay useful when it is not. The order is a path the user
    /// picked, then the ARCHIFY_HOME variable, then the folders the agent skill installers use.
    /// </summary>
    internal static class ArchifyLocator
    {
        public const string PathPreference = "ClarityGameOptimizer.ArchifyPath";
        public const string HomeVariable = "ARCHIFY_HOME";
        public const string ScriptRelativePath = "bin/archify.mjs";

        private static readonly string[] SkillFolders =
        {
            ".claude/skills/archify",
            ".codex/skills/archify",
            ".agents/skills/archify",
            ".config/opencode/skills/archify",
            ".cursor/skills/archify",
        };

        /// <summary>The first candidate that holds the entry script, or null.</summary>
        public static ArchifyLocation Find()
        {
            foreach (KeyValuePair<string, string> candidate in Candidates())
            {
                ArchifyLocation location = At(candidate.Value, candidate.Key);
                if (location != null)
                {
                    return location;
                }
            }

            return null;
        }

        /// <summary>A location for a folder the user pointed at, accepting the repository root, the skill folder or the bin folder; null when no script is there.</summary>
        public static ArchifyLocation At(string folder, string source)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                return null;
            }

            foreach (string root in new[] { folder, Path.Combine(folder, "archify"), Path.GetDirectoryName(folder) ?? "" })
            {
                if (root.Length > 0 && File.Exists(Path.Combine(root, ScriptRelativePath)))
                {
                    return new ArchifyLocation(Path.GetFullPath(root), source);
                }
            }

            return null;
        }

        /// <summary>Where to look, in order, as (source, folder) pairs; empty entries are skipped.</summary>
        public static IEnumerable<KeyValuePair<string, string>> Candidates()
        {
            string preferred = EditorPrefs.GetString(PathPreference, "");
            if (preferred.Length > 0)
            {
                yield return new KeyValuePair<string, string>("Preferences", preferred);
            }

            string home = Environment.GetEnvironmentVariable(HomeVariable);
            if (!string.IsNullOrEmpty(home))
            {
                yield return new KeyValuePair<string, string>(HomeVariable, home);
            }

            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile))
            {
                foreach (string folder in SkillFolders)
                {
                    yield return new KeyValuePair<string, string>("skill folder", Path.Combine(profile, folder));
                }
            }
        }

        public static void Remember(string folder)
        {
            EditorPrefs.SetString(PathPreference, folder ?? "");
        }
    }

    /// <summary>An archify checkout: the folder that holds bin/archify.mjs, and where it was found.</summary>
    internal sealed class ArchifyLocation
    {
        public ArchifyLocation(string root, string source)
        {
            Root = root ?? throw new ArgumentNullException(nameof(root));
            Source = source ?? "";
        }

        public string Root { get; }

        public string Source { get; }

        public string Script
        {
            get { return Path.Combine(Root, ArchifyLocator.ScriptRelativePath.Replace('/', Path.DirectorySeparatorChar)); }
        }
    }
}
