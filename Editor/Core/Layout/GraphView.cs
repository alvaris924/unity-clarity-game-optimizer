using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A named subset of a curated graph worth focusing on, such as one boundary; becomes a view in the rendered diagram.</summary>
    internal sealed class GraphView
    {
        public GraphView(string id, string label, IReadOnlyList<string> nodeIds)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A view needs an id.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("A view needs a label.", nameof(label));
            }

            Id = id;
            Label = label;
            NodeIds = nodeIds ?? throw new ArgumentNullException(nameof(nodeIds));
        }

        public string Id { get; }

        public string Label { get; }

        public IReadOnlyList<string> NodeIds { get; }
    }
}
