using System;
using System.Collections.Generic;
using System.IO;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Settings;
using UnityEditor;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads the project's own scripts for the Code Quality report: every C# file under Assets except Editor folders and
    /// the third-party folders named in Project Settings. Everything that decides what the report says lives in
    /// <see cref="CodeQualityReport"/>.
    /// </summary>
    internal sealed class CodeQualityAnalyzer
    {
        public Report Scan()
        {
            Report report = CodeQualityReport.Build(ProjectScripts(ClarityGameOptimizerSettings.instance.ThirdPartyFolders));
            ReportEnvironment.Stamp(report);
            return report;
        }

        internal static List<SourceFileDescription> ProjectScripts(IReadOnlyList<string> thirdPartyFolders)
        {
            var files = new List<SourceFileDescription>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var progress = new ScanProgress();
            try
            {
                string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
                for (int i = 0; i < guids.Length; i++)
                {
                    progress.Report("Reading scripts", i, guids.Length);
                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) || !seen.Add(path) || IsEditorScript(path) || FolderRules.IsUnderAny(path, thirdPartyFolders))
                    {
                        continue;
                    }

                    try
                    {
                        files.Add(new SourceFileDescription(path, File.ReadAllText(Path.Combine(ReportFolders.ProjectRoot, path))));
                    }
                    catch (IOException)
                    {
                        // An unreadable file is one fewer place to look, not a failed scan.
                    }
                }
            }
            finally
            {
                progress.Clear();
            }

            return files;
        }

        // Folder names that hold an asset pack's demos and samples, which never reach a player build.
        private static readonly string[] SampleFolders = { "/Demo/", "/Demos/", "/Example/", "/Examples/", "/Sample/", "/Samples/", "/For demo scenes/" };

        /// <summary>Editor scripts, and the demos and samples of asset packs: neither runs in a player.</summary>
        internal static bool IsEditorScript(string path)
        {
            if (path.IndexOf("/Editor/", StringComparison.Ordinal) >= 0 || path.StartsWith("Assets/Editor/", StringComparison.Ordinal))
            {
                return true;
            }

            foreach (string folder in SampleFolders)
            {
                if (path.IndexOf(folder, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
