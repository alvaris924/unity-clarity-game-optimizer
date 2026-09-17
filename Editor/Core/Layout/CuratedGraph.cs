using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>What <see cref="GraphCuration"/> produces: the smaller graph, the views into it, and how much was folded away.</summary>
    internal sealed class CuratedGraph
    {
        public CuratedGraph(ReportGraph graph, IReadOnlyList<GraphView> views, int collapsedNodes, int mergedEdges)
        {
            Graph = graph ?? throw new ArgumentNullException(nameof(graph));
            Views = views ?? throw new ArgumentNullException(nameof(views));
            CollapsedNodes = collapsedNodes;
            MergedEdges = mergedEdges;
        }

        public ReportGraph Graph { get; }

        public IReadOnlyList<GraphView> Views { get; }

        /// <summary>Nodes of the original graph that were folded into an "Other" node.</summary>
        public int CollapsedNodes { get; }

        /// <summary>Edges of the original graph that ended up sharing a curated edge with another, or vanished inside a folded node.</summary>
        public int MergedEdges { get; }
    }
}
