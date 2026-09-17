using System;
using System.Collections.Generic;
using System.Text;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes a graph as a Mermaid flowchart, the full graph with nothing folded, which GitHub renders
    /// inline in Markdown. Groups become subgraphs. Mermaid identifiers cannot carry hyphens or its own
    /// keywords, so node ids are slugged with underscores and guarded.
    /// </summary>
    internal static class MermaidExporter
    {
        private static readonly HashSet<string> Reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "end", "graph", "subgraph", "flowchart", "style", "classdef", "class", "click", "linkstyle", "direction",
        };

        public static string Write(ReportGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var ids = new DiagramIds(graph, MermaidId);
            var text = new StringBuilder();
            text.Append("flowchart LR\n");

            var groups = new List<string>();
            var members = new Dictionary<string, List<GraphNode>>(StringComparer.Ordinal);
            var ungrouped = new List<GraphNode>();
            foreach (GraphNode node in graph.Nodes)
            {
                if (node.Group.Length == 0)
                {
                    ungrouped.Add(node);
                    continue;
                }

                List<GraphNode> list;
                if (!members.TryGetValue(node.Group, out list))
                {
                    list = new List<GraphNode>();
                    members.Add(node.Group, list);
                    groups.Add(node.Group);
                }

                list.Add(node);
            }

            foreach (string group in groups)
            {
                text.Append("  subgraph ").Append(ids.Reserve("group " + group)).Append("[").Append(Label(group)).Append("]\n");
                foreach (GraphNode node in members[group])
                {
                    text.Append("    ");
                    AppendNode(text, ids, node);
                }

                text.Append("  end\n");
            }

            foreach (GraphNode node in ungrouped)
            {
                text.Append("  ");
                AppendNode(text, ids, node);
            }

            foreach (GraphEdge edge in graph.Edges)
            {
                if (!ids.Has(edge.From) || !ids.Has(edge.To))
                {
                    continue;
                }

                text.Append("  ").Append(ids.Of(edge.From));
                if (edge.Label.Length > 0)
                {
                    text.Append(" -->|").Append(edge.Label.Replace('|', '/').Replace('\n', ' ').Replace('\r', ' ')).Append("| ");
                }
                else
                {
                    text.Append(" --> ");
                }

                text.Append(ids.Of(edge.To)).Append('\n');
            }

            return text.ToString();
        }

        /// <summary>Hyphens become underscores and reserved words get a trailing underscore.</summary>
        public static string MermaidId(string slug)
        {
            string id = slug.Replace('-', '_');
            return Reserved.Contains(id) ? id + "_" : id;
        }

        private static void AppendNode(StringBuilder text, DiagramIds ids, GraphNode node)
        {
            string label = node.Sublabel.Length > 0 ? node.Label + "<br/>" + node.Sublabel : node.Label;
            text.Append(ids.Of(node.Id)).Append("[").Append(Label(label)).Append("]\n");
        }

        /// <summary>A quoted Mermaid label; quotes inside become the entity Mermaid understands.</summary>
        public static string Label(string value)
        {
            return "\"" + value.Replace("\"", "#quot;").Replace('\n', ' ').Replace('\r', ' ') + "\"";
        }
    }
}
