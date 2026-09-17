using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Shrinks a graph to what a diagram can show: the heaviest nodes stay, with the nodes the diagram is
    /// about ranked before the rest when the caller says which those are, every other node folds into one
    /// "Other" node per group, edges follow their nodes and merge with a count, and one view per group
    /// points at what is left. archify wants about twelve primary nodes and at most five views; a real
    /// project has hundreds of assemblies, so this is the step between the report and the picture. The
    /// full graph is untouched and still goes out as DOT and Mermaid.
    /// </summary>
    internal static class GraphCuration
    {
        public const int DefaultMaxNodes = 12;
        public const int MaxViews = 5;

        private const string OtherIdPrefix = "other:";

        /// <param name="isPrimary">Nodes the diagram is about, kept before any other; null ranks every node by weight alone.</param>
        /// <param name="secondarySlots">How many of the kept nodes are held back for the heaviest non-primary nodes, so the diagram still shows what the primary ones lean on.</param>
        public static CuratedGraph Curate(ReportGraph graph, int maxNodes = DefaultMaxNodes, Func<GraphNode, bool> isPrimary = null, int secondarySlots = 0)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            if (maxNodes < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNodes), "At least one node must be kept.");
            }

            if (secondarySlots < 0 || secondarySlots > maxNodes)
            {
                throw new ArgumentOutOfRangeException(nameof(secondarySlots), "Secondary slots must fit inside the node limit.");
            }

            HashSet<string> kept = Keep(graph, maxNodes, isPrimary, secondarySlots);

            var curated = new ReportGraph();
            var target = new Dictionary<string, string>(StringComparer.Ordinal);
            var folded = new Dictionary<string, List<GraphNode>>(StringComparer.Ordinal);
            var groupOrder = new List<string>();
            foreach (GraphNode node in graph.Nodes)
            {
                if (!groupOrder.Contains(node.Group))
                {
                    groupOrder.Add(node.Group);
                }

                if (kept.Contains(node.Id))
                {
                    curated.AddNode(Copy(node));
                    target[node.Id] = node.Id;
                    continue;
                }

                List<GraphNode> members;
                if (!folded.TryGetValue(node.Group, out members))
                {
                    members = new List<GraphNode>();
                    folded.Add(node.Group, members);
                }

                members.Add(node);
                target[node.Id] = OtherIdPrefix + node.Group;
            }

            int collapsed = 0;
            foreach (string group in groupOrder)
            {
                List<GraphNode> members;
                if (folded.TryGetValue(group, out members))
                {
                    curated.AddNode(OtherNode(group, members));
                    collapsed += members.Count;
                }
            }

            int merged = 0;
            var mergedEdges = new Dictionary<string, GraphEdge>(StringComparer.Ordinal);
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (GraphEdge edge in graph.Edges)
            {
                string from;
                string to;
                if (!target.TryGetValue(edge.From, out from) || !target.TryGetValue(edge.To, out to))
                {
                    continue;
                }

                if (from == to)
                {
                    merged++;
                    continue;
                }

                string key = from + "\n" + to;
                GraphEdge existing;
                if (mergedEdges.TryGetValue(key, out existing))
                {
                    existing.Weight += edge.Weight;
                    counts[key]++;
                    merged++;
                    continue;
                }

                mergedEdges.Add(key, new GraphEdge(from, to) { Label = edge.Label, Weight = edge.Weight });
                counts.Add(key, 1);
                order.Add(key);
            }

            foreach (string key in order)
            {
                GraphEdge edge = mergedEdges[key];
                int count = counts[key];
                if (count > 1 && edge.Label.Length > 0)
                {
                    edge.Label = count.ToString(CultureInfo.InvariantCulture) + " " + edge.Label;
                }

                curated.AddEdge(edge);
            }

            return new CuratedGraph(curated, Views(curated, groupOrder), collapsed, merged);
        }

        /// <summary>The ids that survive: primary nodes by weight into the slots not held back, then every other node by weight into what is left.</summary>
        private static HashSet<string> Keep(ReportGraph graph, int maxNodes, Func<GraphNode, bool> isPrimary, int secondarySlots)
        {
            var kept = new HashSet<string>(StringComparer.Ordinal);
            if (isPrimary == null)
            {
                var ranked = new List<GraphNode>(graph.Nodes);
                ranked.Sort(CompareByWeight);
                for (int i = 0; i < ranked.Count && i < maxNodes; i++)
                {
                    kept.Add(ranked[i].Id);
                }

                return kept;
            }

            var primary = new List<GraphNode>();
            var secondary = new List<GraphNode>();
            foreach (GraphNode node in graph.Nodes)
            {
                (isPrimary(node) ? primary : secondary).Add(node);
            }

            primary.Sort(CompareByWeight);
            secondary.Sort(CompareByWeight);
            int primarySlots = Math.Min(primary.Count, maxNodes - secondarySlots);
            for (int i = 0; i < primarySlots; i++)
            {
                kept.Add(primary[i].Id);
            }

            for (int i = 0; i < secondary.Count && kept.Count < maxNodes; i++)
            {
                kept.Add(secondary[i].Id);
            }

            return kept;
        }

        private static List<GraphView> Views(ReportGraph curated, List<string> groupOrder)
        {
            var views = new List<GraphView>();
            foreach (string group in groupOrder)
            {
                if (views.Count == MaxViews)
                {
                    break;
                }

                var ids = new List<string>();
                foreach (GraphNode node in curated.Nodes)
                {
                    if (node.Group == group)
                    {
                        ids.Add(node.Id);
                    }
                }

                if (ids.Count > 0)
                {
                    string label = group.Length > 0 ? group : "Ungrouped";
                    views.Add(new GraphView(label, label, ids));
                }
            }

            return views;
        }

        private static GraphNode OtherNode(string group, List<GraphNode> members)
        {
            string count = members.Count.ToString(CultureInfo.InvariantCulture);
            string label = group.Length > 0 ? "Other " + group + " (" + count + ")" : "Other (" + count + ")";
            var node = new GraphNode(OtherIdPrefix + group, label, MajorityKind(members)) { Group = group };
            foreach (GraphNode member in members)
            {
                node.Weight += member.Weight;
            }

            return node;
        }

        /// <summary>The kind most members have; ties go to the kind that comes first in the enum.</summary>
        private static NodeKind MajorityKind(List<GraphNode> members)
        {
            var tally = new Dictionary<NodeKind, int>();
            foreach (GraphNode member in members)
            {
                int current;
                tally.TryGetValue(member.Kind, out current);
                tally[member.Kind] = current + 1;
            }

            NodeKind best = NodeKind.Runtime;
            int bestCount = -1;
            foreach (NodeKind kind in Enum.GetValues(typeof(NodeKind)))
            {
                int count;
                if (tally.TryGetValue(kind, out count) && count > bestCount)
                {
                    best = kind;
                    bestCount = count;
                }
            }

            return best;
        }

        private static GraphNode Copy(GraphNode node)
        {
            var copy = new GraphNode(node.Id, node.Label, node.Kind)
            {
                Sublabel = node.Sublabel,
                Group = node.Group,
                Weight = node.Weight,
            };
            copy.Evidence.AddRange(node.Evidence);
            return copy;
        }

        private static int CompareByWeight(GraphNode a, GraphNode b)
        {
            int byWeight = b.Weight.CompareTo(a.Weight);
            if (byWeight != 0)
            {
                return byWeight;
            }

            int byLabel = string.CompareOrdinal(a.Label, b.Label);
            return byLabel != 0 ? byLabel : string.CompareOrdinal(a.Id, b.Id);
        }
    }
}
