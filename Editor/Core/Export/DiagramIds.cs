using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// One stable mapping from graph node ids to the identifiers a diagram format accepts, shared by every
    /// part of one export so a node, its edges, its boundary and its views all agree. Two nodes whose
    /// names slug to the same identifier get a numeric suffix, in node order.
    /// </summary>
    internal sealed class DiagramIds
    {
        private readonly Dictionary<string, string> _byNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly HashSet<string> _taken = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<string, string> _transform;

        /// <param name="transform">An extra step after slugging, for formats stricter than archify's rule; null for none.</param>
        public DiagramIds(ReportGraph graph, Func<string, string> transform = null)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }

            _transform = transform;
            foreach (GraphNode node in graph.Nodes)
            {
                _byNodeId.Add(node.Id, Reserve(node.Id));
            }
        }

        /// <summary>The identifier of a node of the graph this was built from.</summary>
        public string Of(string nodeId)
        {
            string id;
            if (!_byNodeId.TryGetValue(nodeId, out id))
            {
                throw new KeyNotFoundException("The graph has no node '" + nodeId + "'.");
            }

            return id;
        }

        public bool Has(string nodeId)
        {
            return nodeId != null && _byNodeId.ContainsKey(nodeId);
        }

        /// <summary>A fresh identifier for something that is not a node, such as a view or a group, unique against everything reserved so far.</summary>
        public string Reserve(string preferred)
        {
            string slug = IdentifierSlug.From(preferred);
            if (_transform != null)
            {
                slug = _transform(slug);
            }

            return IdentifierSlug.Unique(slug, _taken);
        }
    }
}
