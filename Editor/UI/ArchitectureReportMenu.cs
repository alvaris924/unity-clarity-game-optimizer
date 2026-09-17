using System.Globalization;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// Runs the Architecture scan from the menu and writes the report files. A stand-in until the window
    /// exists; the window will own scanning and exporting, and this item goes away with it.
    /// </summary>
    internal static class ArchitectureReportMenu
    {
        private const string MenuPath = "Tools/Clarity Game Optimizer/Write Architecture Report";
        private const string LogPrefix = "[ClarityGameOptimizer] ";

        [MenuItem(MenuPath, false, 100)]
        private static void WriteArchitectureReport()
        {
            Report report = new AssemblyGraphAnalyzer().Scan();
            ReportFiles files = ReportWriter.Write(report, ReportFolders.ForArea(report.Area));
            Debug.Log(LogPrefix + "Architecture: "
                + report.Graph.Nodes.Count.ToString(CultureInfo.InvariantCulture) + " assemblies, "
                + report.Graph.Edges.Count.ToString(CultureInfo.InvariantCulture) + " references, "
                + report.Findings.Count.ToString(CultureInfo.InvariantCulture) + " findings. Report written to "
                + files.Json);
        }
    }
}
