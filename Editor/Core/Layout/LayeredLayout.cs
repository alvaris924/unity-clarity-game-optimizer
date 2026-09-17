using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Places a graph on a grid so that dependencies read left to right: a node's column is the longest
    /// path leading to it from a node nothing depends on, so the things everything rests on end up in the
    /// rightmost columns. Within a column, nodes of the same group sit together, heaviest first. Edges
    /// that would close a cycle are ignored for the layering, deterministically, and counted. archify has
    /// no automatic layout for architecture diagrams, so this is what fills its row and column fields.
    /// </summary>
    internal static class LayeredLayout
    {
        private enum Visit
        {
            Unseen = 0,
            Open = 1,
            Done = 2,
        }

        public static GraphLayout Compute(ReportGraph graph)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            IReadOnlyList<GraphNode> nodes = graph.Nodes;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < nodes.Count; i++)
            {
                index.Add(nodes[i].Id, i);
            }

            List<int>[] outgoing = CollectEdges(graph, index);
            int backEdges = RemoveBackEdges(outgoing);
            int[] column = LongestPathColumns(outgoing);

            int columns = 0;
            for (int i = 0; i < column.Length; i++)
            {
                columns = Math.Max(columns, column[i] + 1);
            }

            var positions = new Dictionary<string, GridPosition>(StringComparer.Ordinal);
            var rowsPerColumn = new int[columns];
            for (int c = 0; c < columns; c++)
            {
                var members = new List<GraphNode>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (column[i] == c)
                    {
                        members.Add(nodes[i]);
                    }
                }

                members.Sort(CompareWithinColumn);
                for (int row = 0; row < members.Count; row++)
                {
                    positions.Add(members[row].Id, new GridPosition(c, row));
                }

                rowsPerColumn[c] = members.Count;
            }

            return new GraphLayout(positions, rowsPerColumn, backEdges);
        }

        /// <summary>Adjacency lists in edge order, without self-loops, duplicates or edges to nodes the graph lacks.</summary>
        private static List<int>[] CollectEdges(ReportGraph graph, Dictionary<string, int> index)
        {
            var outgoing = new List<int>[index.Count];
            for (int i = 0; i < outgoing.Length; i++)
            {
                outgoing[i] = new List<int>();
            }

            foreach (GraphEdge edge in graph.Edges)
            {
                int from;
                int to;
                if (!index.TryGetValue(edge.From, out from) || !index.TryGetValue(edge.To, out to) || from == to)
                {
                    continue;
                }

                if (!outgoing[from].Contains(to))
                {
                    outgoing[from].Add(to);
                }
            }

            return outgoing;
        }

        /// <summary>Depth-first search from every node in order; an edge into a node still on the search path closes a cycle and is dropped.</summary>
        private static int RemoveBackEdges(List<int>[] outgoing)
        {
            var state = new Visit[outgoing.Length];
            var path = new Stack<KeyValuePair<int, int>>();
            int removed = 0;
            for (int root = 0; root < outgoing.Length; root++)
            {
                if (state[root] != Visit.Unseen)
                {
                    continue;
                }

                state[root] = Visit.Open;
                path.Push(new KeyValuePair<int, int>(root, 0));
                while (path.Count > 0)
                {
                    KeyValuePair<int, int> frame = path.Pop();
                    int node = frame.Key;
                    int next = frame.Value;
                    List<int> targets = outgoing[node];
                    if (next >= targets.Count)
                    {
                        state[node] = Visit.Done;
                        continue;
                    }

                    int target = targets[next];
                    if (state[target] == Visit.Open)
                    {
                        targets.RemoveAt(next);
                        removed++;
                        path.Push(new KeyValuePair<int, int>(node, next));
                        continue;
                    }

                    path.Push(new KeyValuePair<int, int>(node, next + 1));
                    if (state[target] == Visit.Unseen)
                    {
                        state[target] = Visit.Open;
                        path.Push(new KeyValuePair<int, int>(target, 0));
                    }
                }
            }

            return removed;
        }

        /// <summary>Kahn's algorithm over the acyclic graph; a node's column is one past the deepest node that points at it.</summary>
        private static int[] LongestPathColumns(List<int>[] outgoing)
        {
            int count = outgoing.Length;
            var remaining = new int[count];
            for (int from = 0; from < count; from++)
            {
                foreach (int to in outgoing[from])
                {
                    remaining[to]++;
                }
            }

            var column = new int[count];
            var ready = new Queue<int>();
            for (int i = 0; i < count; i++)
            {
                if (remaining[i] == 0)
                {
                    ready.Enqueue(i);
                }
            }

            while (ready.Count > 0)
            {
                int node = ready.Dequeue();
                foreach (int to in outgoing[node])
                {
                    column[to] = Math.Max(column[to], column[node] + 1);
                    if (--remaining[to] == 0)
                    {
                        ready.Enqueue(to);
                    }
                }
            }

            return column;
        }

        private static int CompareWithinColumn(GraphNode a, GraphNode b)
        {
            int byGroup = string.CompareOrdinal(a.Group, b.Group);
            if (byGroup != 0)
            {
                return byGroup;
            }

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
