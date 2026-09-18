using System;
using System.Diagnostics;
using System.IO;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// What the window's buttons do: scan an area, write the report and diagram files, render the diagram
    /// with archify, open things. Every action reports through the returned text and the console, never a
    /// dialog, so it works the same from a button and from a test.
    /// </summary>
    internal static class ReportActions
    {
        public const string LogPrefix = "[ClarityGameOptimizer] ";

        /// <summary>Runs the area's analyzer on the scope and stores the report in the view model.</summary>
        public static Report Scan(OptimizerViewModel model, AreaDescriptor descriptor, string scope = null)
        {
            var watch = Stopwatch.StartNew();
            Report report = descriptor.Scan(scope);
            watch.Stop();
            model.Store(report, watch.Elapsed);
            UnityEngine.Debug.Log(LogPrefix + descriptor.Title + ": " + model.ScanSummary);
            return report;
        }

        /// <summary>Writes report.json, report.md and, when the graph has nodes, the three diagram files into the area's report folder.</summary>
        public static WrittenFiles WriteAll(Report report)
        {
            string folder = ReportFolders.ForArea(report.Area);
            ReportFiles files = ReportWriter.Write(report, folder);
            DiagramFiles? diagrams = report.Graph.IsEmpty ? (DiagramFiles?)null : DiagramWriter.Write(report, folder);
            UnityEngine.Debug.Log(LogPrefix + "Files written to " + folder);
            return new WrittenFiles(folder, files, diagrams);
        }

        /// <summary>Validates and renders the diagram with archify. Returns what happened, in one line, for the status bar.</summary>
        public static string Render(WrittenFiles written, out string htmlPath)
        {
            htmlPath = null;
            if (!written.Diagrams.HasValue)
            {
                return "Nothing to draw: the report has no graph.";
            }

            DiagramFiles diagrams = written.Diagrams.Value;
            ArchifyLocation archify = ArchifyLocator.Find();
            if (archify == null)
            {
                string command = "node <archify>/bin/archify.mjs deliver architecture " + CommandLine.Quote(diagrams.Archify) + " " + CommandLine.Quote(diagrams.Html) + " --quality " + ArchifyArchitectureExporter.QualityProfile;
                UnityEngine.Debug.Log(LogPrefix + "archify was not found. Install it with 'npx skills add tt-a1i/archify -g' or pick its folder with Export > Locate archify, then run: " + command);
                return "archify not found: pick its folder with Export > Locate archify. The diagram files are written.";
            }

            string node = ArchifyRunner.NodeVersion();
            if (node == null)
            {
                return "archify is at " + archify.Root + " but Node.js is not on the path.";
            }

            ArchifyResult validation = ArchifyRunner.Validate(archify, DiagramWriter.ArchifyDiagramType, diagrams.Archify);
            if (!validation.Succeeded)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "archify validate " + validation.Summary + "\n" + validation.CommandLine + "\n" + validation.StandardOutput);
                return "archify validate " + validation.Summary + " (details in the console).";
            }

            ArchifyResult delivery = ArchifyRunner.Deliver(archify, DiagramWriter.ArchifyDiagramType, diagrams.Archify, diagrams.Html);
            if (!delivery.Succeeded)
            {
                UnityEngine.Debug.LogWarning(LogPrefix + "archify deliver " + delivery.Summary + "\n" + delivery.CommandLine + "\n" + delivery.StandardOutput);
                return "archify deliver " + delivery.Summary + " (details in the console).";
            }

            htmlPath = diagrams.Html;
            UnityEngine.Debug.Log(LogPrefix + "Diagram rendered with archify (" + archify.Source + ", Node " + node + ") to " + diagrams.Html);
            return "Rendered with archify (Node " + node + ") to " + diagrams.Html;
        }

        /// <summary>Opens a local file with whatever the system uses for it: the default browser for HTML, the file manager for a folder.</summary>
        public static void OpenLocal(string path)
        {
            Application.OpenURL(new Uri(Path.GetFullPath(path)).AbsoluteUri);
        }

        /// <summary>Lets the user pick the archify checkout and remembers it; returns what to say about it.</summary>
        public static string LocateArchify()
        {
            string folder = EditorUtility.OpenFolderPanel("Locate archify", "", "");
            if (string.IsNullOrEmpty(folder))
            {
                return null;
            }

            ArchifyLocation location = ArchifyLocator.At(folder, "Preferences");
            if (location == null)
            {
                return "No " + ArchifyLocator.ScriptRelativePath + " under " + folder + ". Point at the archify checkout, its archify subfolder or its bin folder.";
            }

            ArchifyLocator.Remember(location.Root);
            return "archify found at " + location.Root + " and remembered for this user.";
        }

        /// <summary>What the status bar says about archify before anything is rendered.</summary>
        public static string ArchifyStatus()
        {
            ArchifyLocation archify = ArchifyLocator.Find();
            return archify == null ? "archify: not found (Export > Locate archify)" : "archify: " + archify.Root + " (" + archify.Source + ")";
        }

        /// <summary>Pings the first evidence of a finding in the Project window, when it is an asset.</summary>
        public static bool PingEvidence(Finding finding)
        {
            foreach (Evidence evidence in finding.Evidence)
            {
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(evidence.Path);
                if (asset != null)
                {
                    EditorGUIUtility.PingObject(asset);
                    Selection.activeObject = asset;
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>The files one WriteAll produced.</summary>
    internal readonly struct WrittenFiles
    {
        public WrittenFiles(string folder, ReportFiles report, DiagramFiles? diagrams)
        {
            Folder = folder;
            Report = report;
            Diagrams = diagrams;
        }

        public string Folder { get; }

        public ReportFiles Report { get; }

        public DiagramFiles? Diagrams { get; }
    }
}
