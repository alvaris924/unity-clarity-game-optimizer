using System;
using System.Collections.Generic;
using System.Text;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes a graph in Graphviz DOT, the full graph with nothing folded, for any DOT viewer. Groups become
    /// clusters, kinds become fill colours, and the sublabel goes on a second line. Identifiers are quoted,
    /// so any node id works as it is.
    /// </summary>
    internal static class DotExporter
    {
        public static string Write(ReportGraph graph, string title)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            var text = new StringBuilder();
            text.Append("digraph ").Append(Quote(string.IsNullOrWhiteSpace(title) ? "graph" : title)).Append(" {\n");
            text.Append("  rankdir=LR;\n");
            text.Append("  node [shape=box, style=\"rounded,filled\", fontname=\"Helvetica\", fontsize=11];\n");
            text.Append("  edge [color=\"#64748b\", fontname=\"Helvetica\", fontsize=9];\n");

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
                text.Append("  subgraph ").Append(Quote("cluster_" + group)).Append(" {\n");
                text.Append("    label=").Append(Quote(group)).Append(";\n");
                text.Append("    style=\"rounded\"; color=\"#94a3b8\";\n");
                foreach (GraphNode node in members[group])
                {
                    text.Append("    ");
                    AppendNode(text, node);
                }

                text.Append("  }\n");
            }

            foreach (GraphNode node in ungrouped)
            {
                text.Append("  ");
                AppendNode(text, node);
            }

            foreach (GraphEdge edge in graph.Edges)
            {
                text.Append("  ").Append(Quote(edge.From)).Append(" -> ").Append(Quote(edge.To));
                if (edge.Label.Length > 0)
                {
                    text.Append(" [label=").Append(Quote(edge.Label)).Append(']');
                }

                text.Append(";\n");
            }

            text.Append("}\n");
            return text.ToString();
        }

        /// <summary>A light fill per kind so the roles read at a glance.</summary>
        public static string FillColor(NodeKind kind)
        {
            switch (kind)
            {
                case NodeKind.Editor:
                    return "#ede9fe";
                case NodeKind.Data:
                    return "#fef3c7";
                case NodeKind.Service:
                    return "#cffafe";
                case NodeKind.Messaging:
                    return "#fce7f3";
                case NodeKind.Test:
                    return "#dcfce7";
                case NodeKind.External:
                    return "#f1f5f9";
                default:
                    return "#dbeafe";
            }
        }

        private static void AppendNode(StringBuilder text, GraphNode node)
        {
            string label = node.Sublabel.Length > 0 ? node.Label + "\n" + node.Sublabel : node.Label;
            text.Append(Quote(node.Id)).Append(" [label=").Append(Quote(label)).Append(", fillcolor=\"").Append(FillColor(node.Kind)).Append("\"];\n");
        }

        /// <summary>A DOT double-quoted string: backslashes and quotes escaped, line breaks as the two characters backslash and n.</summary>
        public static string Quote(string value)
        {
            var text = new StringBuilder(value.Length + 2);
            text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        text.Append("\\\"");
                        break;
                    case '\\':
                        text.Append("\\\\");
                        break;
                    case '\n':
                        text.Append("\\n");
                        break;
                    case '\r':
                        break;
                    default:
                        text.Append(c);
                        break;
                }
            }

            text.Append('"');
            return text.ToString();
        }
    }
}
