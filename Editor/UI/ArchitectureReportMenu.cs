using System.Globalization;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// Runs the Architecture scan from the menu, writes the report and diagram files, and renders the
    /// diagram when archify and Node are on this machine. A stand-in until the window exists; the window
    /// will own scanning, exporting and rendering, and these items go away with it.
    /// </summary>
    internal static class ArchitectureReportMenu
    {
        private const string MenuRoot = "Tools/Clarity Game Optimizer/";
        private const string LogPrefix = "[ClarityGameOptimizer] ";

        [MenuItem(MenuRoot + "Write Architecture Report", false, 100)]
        private static void WriteArchitectureReport()
        {
            Report report = new AssemblyGraphAnalyzer().Scan();
            string folder = ReportFolders.ForArea(report.Area);
            ReportFiles files = ReportWriter.Write(report, folder);
            Debug.Log(LogPrefix + "Architecture: "
                + Count(report.Graph.Nodes.Count) + " assemblies, "
                + Count(report.Graph.Edges.Count) + " references, "
                + Count(report.Findings.Count) + " findings. Report written to " + files.Json);

            if (report.Graph.IsEmpty)
            {
                Debug.Log(LogPrefix + "No assemblies to draw.");
                return;
            }

            DiagramFiles diagrams = DiagramWriter.Write(report, folder);
            Debug.Log(LogPrefix + "Diagram files written: " + diagrams.Archify + ", " + diagrams.Dot + ", " + diagrams.Mermaid);
            Render(diagrams);
        }

        [MenuItem(MenuRoot + "Locate archify...", false, 200)]
        private static void LocateArchify()
        {
            string folder = EditorUtility.OpenFolderPanel("Locate archify", "", "");
            if (string.IsNullOrEmpty(folder))
            {
                return;
            }

            ArchifyLocation location = ArchifyLocator.At(folder, "Preferences");
            if (location == null)
            {
                EditorUtility.DisplayDialog("Locate archify", "No " + ArchifyLocator.ScriptRelativePath + " under that folder. Point at the archify checkout, its archify subfolder or its bin folder.", "OK");
                return;
            }

            ArchifyLocator.Remember(location.Root);
            Debug.Log(LogPrefix + "archify found at " + location.Root + " and remembered for this user.");
        }

        private static void Render(DiagramFiles diagrams)
        {
            ArchifyLocation archify = ArchifyLocator.Find();
            if (archify == null)
            {
                Debug.Log(LogPrefix + "archify was not found, so the diagram was not rendered. Install it with 'npx skills add tt-a1i/archify -g' or pick its folder with Tools > Clarity Game Optimizer > Locate archify, then run: node <archify>/bin/archify.mjs deliver architecture "
                    + CommandLine.Quote(diagrams.Archify) + " " + CommandLine.Quote(diagrams.Html) + " --quality " + ArchifyArchitectureExporter.QualityProfile);
                return;
            }

            string node = ArchifyRunner.NodeVersion();
            if (node == null)
            {
                Debug.LogWarning(LogPrefix + "archify is at " + archify.Root + " but Node.js was not found on the path, so the diagram was not rendered.");
                return;
            }

            ArchifyResult validation = ArchifyRunner.Validate(archify, DiagramWriter.ArchifyDiagramType, diagrams.Archify);
            if (!validation.Succeeded)
            {
                Debug.LogWarning(LogPrefix + "archify validate " + validation.Summary + "\n" + validation.CommandLine + "\n" + validation.StandardOutput);
                return;
            }

            ArchifyResult delivery = ArchifyRunner.Deliver(archify, DiagramWriter.ArchifyDiagramType, diagrams.Archify, diagrams.Html);
            if (!delivery.Succeeded)
            {
                Debug.LogWarning(LogPrefix + "archify deliver " + delivery.Summary + "\n" + delivery.CommandLine + "\n" + delivery.StandardOutput);
                return;
            }

            Debug.Log(LogPrefix + "Diagram rendered with archify (" + archify.Source + ", Node " + node + ") to " + diagrams.Html);
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
