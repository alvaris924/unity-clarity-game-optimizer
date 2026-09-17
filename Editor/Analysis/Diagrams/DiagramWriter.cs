using System;
using System.IO;
using System.Text;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Writes the diagram files of an Architecture report next to its report files: the archify IR of the
    /// curated graph, and the full graph as DOT and Mermaid, which need no tool at all.
    /// </summary>
    internal static class DiagramWriter
    {
        public const string BaseName = "assemblies";
        public const string ArchifyDiagramType = "architecture";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static DiagramFiles Write(Report report, string folder)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A folder is needed.", nameof(folder));
            }

            Directory.CreateDirectory(folder);
            string archifyPath = Path.Combine(folder, BaseName + "." + ArchifyDiagramType + ".json");
            string dotPath = Path.Combine(folder, BaseName + ".dot");
            string mermaidPath = Path.Combine(folder, BaseName + ".mmd");
            File.WriteAllText(archifyPath, ArchifyArchitectureExporter.Export(report) + "\n", Utf8WithoutBom);
            File.WriteAllText(dotPath, DotExporter.Write(report.Graph, ArchifyArchitectureExporter.DefaultTitle), Utf8WithoutBom);
            File.WriteAllText(mermaidPath, MermaidExporter.Write(report.Graph), Utf8WithoutBom);
            return new DiagramFiles(archifyPath, dotPath, mermaidPath, Path.Combine(folder, BaseName + ".html"));
        }
    }

    /// <summary>The diagram files one write produced, and where the rendered HTML goes.</summary>
    internal readonly struct DiagramFiles
    {
        public DiagramFiles(string archify, string dot, string mermaid, string html)
        {
            Archify = archify;
            Dot = dot;
            Mermaid = mermaid;
            Html = html;
        }

        public string Archify { get; }

        public string Dot { get; }

        public string Mermaid { get; }

        public string Html { get; }
    }
}
