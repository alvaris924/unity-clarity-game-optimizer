using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// The nodes and edges a report carries, kept in insertion order so two runs over the same project
    /// produce the same output. Node ids are unique; an edge may name a node that is added later, and the
    /// validator reports any that never turns up.
    /// </summary>
    internal sealed class ReportGraph
    {
        private readonly List<GraphNode> _nodes = new List<GraphNode>();
        private readonly Dictionary<string, GraphNode> _byId = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        private readonly List<GraphEdge> _edges = new List<GraphEdge>();

        public IReadOnlyList<GraphNode> Nodes
        {
            get { return _nodes; }
        }

        public IReadOnlyList<GraphEdge> Edges
        {
            get { return _edges; }
        }

        public bool IsEmpty
        {
            get { return _nodes.Count == 0; }
        }

        public GraphNode AddNode(GraphNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }

            if (_byId.ContainsKey(node.Id))
            {
                throw new InvalidOperationException("The graph already has a node with id '" + node.Id + "'.");
            }

            _byId.Add(node.Id, node);
            _nodes.Add(node);
            return node;
        }

        public GraphEdge AddEdge(GraphEdge edge)
        {
            if (edge == null)
            {
                throw new ArgumentNullException(nameof(edge));
            }

            _edges.Add(edge);
            return edge;
        }

        public bool TryGetNode(string id, out GraphNode node)
        {
            if (id == null)
            {
                node = null;
                return false;
            }

            return _byId.TryGetValue(id, out node);
        }
    }
}
