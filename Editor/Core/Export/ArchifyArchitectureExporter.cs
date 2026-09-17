using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes an Architecture report as an archify <c>architecture</c> diagram (schema version 1): the
    /// curated graph as grid-placed components typed through a fixed legend and tagged with their group,
    /// one connection per edge, one view per group, and a card with the findings that matter. When the caller
    /// names a public GitHub repository and revision, evidence becomes <c>sources</c> and the viewer's
    /// source capsule leads back to the asmdef; archify verifies those against git, so a private project
    /// gets no capsules. archify validates the result and renders it; nothing here draws.
    ///
    /// Placement: columns come from the layered layout, and every component gets a row of its own, in
    /// column order, so no connection ever runs through an unrelated component and archify's router can
    /// bridge every pair. That is what its <c>standard</c> profile accepts for a dependency graph; the
    /// <c>showcase</c> profile also forbids crossings, which a graph with hubs cannot avoid.
    /// </summary>
    internal static class ArchifyArchitectureExporter
    {
        public const string DefaultTitle = "Assembly map";

        /// <summary>archify's grid supports at most this many columns; deeper layouts are squeezed into it.</summary>
        public const int MaxColumns = 12;

        public const int MaxSources = 3;
        public const int MaxSourcePathLength = 240;
        public const int MaxSourceLabelLength = 48;
        public const int MaxViewLabelLength = 48;
        public const int MaxCardItems = 5;
        public const double MaxLineWidth = 6;

        public const string QualityProfile = "standard";

        private const int MinCellWidth = 120;
        private const int MaxCellWidth = 280;
        private const int CellPadding = 24;
        private const int PixelsPerCharacter = 7;
        private const int CellHeight = 64;
        private const int GapX = 72;
        private const int GapY = 40;
        private const int OriginX = 40;
        private const int OriginY = 100;

        private static readonly KeyValuePair<string, string>[] Legend =
        {
            new KeyValuePair<string, string>("backend", "Runtime code"),
            new KeyValuePair<string, string>("frontend", "Editor tools"),
            new KeyValuePair<string, string>("database", "Data and content"),
            new KeyValuePair<string, string>("cloud", "Backend services"),
            new KeyValuePair<string, string>("messagebus", "Events and messaging"),
            new KeyValuePair<string, string>("security", "Tests"),
            new KeyValuePair<string, string>("external", "Packages and plugins"),
        };

        /// <summary>
        /// Curates and lays out the report's graph, then writes the diagram. The project's own assemblies are
        /// kept first; a quarter of the slots go to the packages and plugins they lean on most.
        /// </summary>
        public static string Export(Report report, int maxNodes = GraphCuration.DefaultMaxNodes, string title = null, ArchifyRepository repository = null)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            CuratedGraph curated = GraphCuration.Curate(report.Graph, maxNodes, node => node.Kind != NodeKind.External, maxNodes / 4);
            return Write(report, curated, LayeredLayout.Compute(curated.Graph), title, repository);
        }

        /// <param name="repository">The public repository the evidence points into, or null to write no sources.</param>
        public static string Write(Report report, CuratedGraph curated, GraphLayout layout, string title = null, ArchifyRepository repository = null)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (curated == null)
            {
                throw new ArgumentNullException(nameof(curated));
            }

            if (layout == null)
            {
                throw new ArgumentNullException(nameof(layout));
            }

            ReportGraph graph = curated.Graph;
            if (graph.IsEmpty)
            {
                throw new InvalidOperationException("There is nothing to draw: the graph has no nodes.");
            }

            var ids = new DiagramIds(graph);
            Dictionary<string, GridPosition> cells = Cells(graph, layout);
            int columns = 0;
            foreach (GridPosition cell in cells.Values)
            {
                columns = Math.Max(columns, cell.Column + 1);
            }

            int cellWidth = CellWidthFor(graph);

            var json = new JsonWriter();
            json.BeginObject();
            json.Name("schema_version").Value(1);
            json.Name("diagram_type").Value("architecture");

            json.Name("meta").BeginObject();
            json.Name("title").Value(string.IsNullOrWhiteSpace(title) ? Areas.DiagramTitle(report.Area) : title);
            json.Name("subtitle").Value(Subtitle(report, curated));
            json.Name("quality_profile").Value(QualityProfile);
            if (repository != null)
            {
                json.Name("repository").BeginObject();
                json.Name("url").Value(repository.Url);
                json.Name("revision").Value(repository.Revision);
                json.EndObject();
            }

            json.Name("legend").BeginObject();
            json.Name("mode").Value("auto");
            json.Name("entries").BeginObject();
            foreach (KeyValuePair<string, string> entry in Legend)
            {
                json.Name(entry.Key).BeginObject().Name("label").Value(entry.Value).EndObject();
            }

            json.EndObject();
            json.EndObject();
            WriteViews(json, curated, ids);
            json.EndObject();

            json.Name("layout").BeginObject();
            json.Name("mode").Value("grid");
            json.Name("origin").BeginArray().Value(OriginX).Value(OriginY).EndArray();
            json.Name("cols").Value(columns);
            json.Name("gapX").Value(GapX);
            json.Name("gapY").Value(GapY);
            json.Name("cellW").Value(cellWidth);
            json.Name("cellH").Value(CellHeight);
            json.EndObject();

            json.Name("components").BeginArray();
            foreach (GraphNode node in graph.Nodes)
            {
                GridPosition cell = cells[node.Id];
                json.BeginObject();
                json.Name("id").Value(ids.Of(node.Id));
                json.Name("type").Value(ComponentType(node.Kind));
                json.Name("label").Value(node.Label);
                if (node.Sublabel.Length > 0)
                {
                    json.Name("sublabel").Value(node.Sublabel);
                }

                if (node.Group.Length > 0)
                {
                    json.Name("tag").Value(node.Group);
                }

                if (repository != null)
                {
                    WriteSources(json, node.Evidence);
                }

                json.Name("row").Value(cell.Row);
                json.Name("col").Value(cell.Column);
                json.Name("size").BeginArray().Value(cellWidth).Value(CellHeight).EndArray();
                json.EndObject();
            }

            json.EndArray();

            json.Name("connections").BeginArray();
            foreach (GraphEdge edge in graph.Edges)
            {
                if (!ids.Has(edge.From) || !ids.Has(edge.To))
                {
                    continue;
                }

                json.BeginObject();
                json.Name("from").Value(ids.Of(edge.From));
                json.Name("to").Value(ids.Of(edge.To));
                if (edge.Weight > 1)
                {
                    json.Name("width").Value(LineWidth(edge.Weight));
                }

                if (IsFolded(graph, edge.From) || IsFolded(graph, edge.To))
                {
                    json.Name("variant").Value("dashed");
                }

                json.EndObject();
            }

            json.EndArray();

            WriteCards(json, report);
            json.EndObject();
            return json.ToString();
        }

        /// <summary>The Unity kind of a node as one of archify's seven component types.</summary>
        public static string ComponentType(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Editor:
                    return "frontend";
                case NodeKind.Data:
                    return "database";
                case NodeKind.Service:
                    return "cloud";
                case NodeKind.Messaging:
                    return "messagebus";
                case NodeKind.Test:
                    return "security";
                case NodeKind.External:
                    return "external";
                default:
                    return "backend";
            }
        }

        /// <summary>
        /// The layout's columns, scaled down to the grid's limit when the graph is deeper than that, and one
        /// row per component in column order, so a horizontal segment can only ever meet its own endpoints.
        /// </summary>
        private static Dictionary<string, GridPosition> Cells(ReportGraph graph, GraphLayout layout)
        {
            var ordered = new List<KeyValuePair<GraphNode, GridPosition>>();
            foreach (GraphNode node in graph.Nodes)
            {
                ordered.Add(new KeyValuePair<GraphNode, GridPosition>(node, layout.Position(node.Id)));
            }

            ordered.Sort((a, b) =>
            {
                int byColumn = a.Value.Column.CompareTo(b.Value.Column);
                return byColumn != 0 ? byColumn : a.Value.Row.CompareTo(b.Value.Row);
            });

            double scale = layout.Columns > MaxColumns ? (MaxColumns - 1) / (double)(layout.Columns - 1) : 1;
            var cells = new Dictionary<string, GridPosition>(StringComparer.Ordinal);
            int row = 0;
            foreach (KeyValuePair<GraphNode, GridPosition> entry in ordered)
            {
                int column = (int)Math.Round(entry.Value.Column * scale, MidpointRounding.AwayFromZero);
                cells.Add(entry.Key.Id, new GridPosition(column, row));
                row++;
            }

            return cells;
        }

        /// <summary>One width for every cell, wide enough for the longest label at roughly seven pixels a character.</summary>
        private static int CellWidthFor(ReportGraph graph)
        {
            int longest = 0;
            foreach (GraphNode node in graph.Nodes)
            {
                longest = Math.Max(longest, node.Label.Length);
                longest = Math.Max(longest, (int)Math.Ceiling(node.Sublabel.Length * 0.85));
            }

            return Math.Min(MaxCellWidth, Math.Max(MinCellWidth, CellPadding + PixelsPerCharacter * longest));
        }

        private static string Subtitle(Report report, CuratedGraph curated)
        {
            int total = report.Graph.Nodes.Count;
            int drawn = curated.Graph.Nodes.Count;
            string text = total.ToString(CultureInfo.InvariantCulture) + " " + Areas.NodeNoun(report.Area, total);
            if (curated.CollapsedNodes > 0)
            {
                text += ", " + drawn.ToString(CultureInfo.InvariantCulture) + " drawn, " + curated.CollapsedNodes.ToString(CultureInfo.InvariantCulture) + " folded";
            }

            text += " · " + ReportMarkdownExporter.ProductName;
            if (report.PackageVersion.Length > 0)
            {
                text += " " + report.PackageVersion;
            }

            return text + " · " + ReportJsonExporter.FormatTimestamp(report.CreatedAt).Substring(0, 10);
        }

        private static void WriteViews(JsonWriter json, CuratedGraph curated, DiagramIds ids)
        {
            if (curated.Views.Count == 0)
            {
                return;
            }

            json.Name("views").BeginArray();
            foreach (GraphView view in curated.Views)
            {
                var focus = new List<string>();
                foreach (string nodeId in view.NodeIds)
                {
                    if (ids.Has(nodeId))
                    {
                        focus.Add(ids.Of(nodeId));
                    }
                }

                if (focus.Count == 0)
                {
                    continue;
                }

                json.BeginObject();
                json.Name("id").Value(ids.Reserve("view-" + view.Id));
                json.Name("label").Value(Truncate(view.Label, MaxViewLabelLength));
                json.Name("focus").BeginArray();
                foreach (string id in focus)
                {
                    json.Value(id);
                }

                json.EndArray();
                json.EndObject();
            }

            json.EndArray();
        }

        private static void WriteSources(JsonWriter json, List<Evidence> evidence)
        {
            var usable = new List<Evidence>();
            foreach (Evidence item in evidence)
            {
                if (item.Path.Length <= MaxSourcePathLength && usable.Count < MaxSources)
                {
                    usable.Add(item);
                }
            }

            if (usable.Count == 0)
            {
                return;
            }

            json.Name("sources").BeginArray();
            foreach (Evidence item in usable)
            {
                json.BeginObject();
                json.Name("path").Value(item.Path);
                if (item.Line > 0)
                {
                    json.Name("line").Value(item.Line);
                    if (item.EndLine > 0)
                    {
                        json.Name("end_line").Value(item.EndLine);
                    }
                }

                if (item.Label.Length > 0)
                {
                    json.Name("label").Value(Truncate(item.Label, MaxSourceLabelLength));
                }

                json.EndObject();
            }

            json.EndArray();
        }

        private static void WriteCards(JsonWriter json, Report report)
        {
            List<Finding> notable = FindingOrder.MostSevereFirst(report.Findings.Where(finding => finding.Severity > Severity.Info))
                .Take(MaxCardItems)
                .ToList();
            if (notable.Count == 0)
            {
                return;
            }

            json.Name("cards").BeginArray();
            json.BeginObject();
            json.Name("dot").Value(notable[0].Severity == Severity.Critical ? "rose" : "amber");
            json.Name("title").Value("Findings above Info (" + report.Findings.Count(finding => finding.Severity > Severity.Info).ToString(CultureInfo.InvariantCulture) + ")");
            json.Name("items").BeginArray();
            foreach (Finding finding in notable)
            {
                json.Value(finding.Severity + ": " + finding.Subject + " — " + finding.Title);
            }

            json.EndArray();
            json.EndObject();
            json.EndArray();
        }

        /// <summary>A merged connection grows with the number of references it stands for, gently and with a cap; labels would collide at this density.</summary>
        internal static double LineWidth(double references)
        {
            return Math.Round(Math.Min(MaxLineWidth, 1 + Math.Log(references, 2) * 0.75), 1);
        }

        private static bool IsFolded(ReportGraph graph, string nodeId)
        {
            return nodeId.StartsWith("other:", StringComparison.Ordinal);
        }

        private static string Truncate(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }
    }
}
