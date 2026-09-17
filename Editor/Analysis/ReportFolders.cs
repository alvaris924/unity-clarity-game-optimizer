using System.IO;
using ClarityGameOptimizer.Core;
using UnityEngine;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Where reports are written: a folder beside Assets, one subfolder per area, so a report can be
    /// committed for history or ignored, and never becomes an asset the Editor imports.
    /// </summary>
    internal static class ReportFolders
    {
        public const string RootName = "ClarityGameOptimizerReports";

        /// <summary>The project folder, the parent of Assets.</summary>
        public static string ProjectRoot
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        public static string Root
        {
            get { return Path.Combine(ProjectRoot, RootName); }
        }

        public static string ForArea(Area area)
        {
            return Path.Combine(Root, Areas.FolderName(area));
        }
    }
}
