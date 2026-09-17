using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A directed relation between two nodes: a reference, a dependency, a flow of bytes.</summary>
    internal sealed class GraphEdge
    {
        private string _label = "";

        public GraphEdge(string from, string to)
        {
            if (string.IsNullOrWhiteSpace(from))
            {
                throw new ArgumentException("An edge needs a source node id.", nameof(from));
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                throw new ArgumentException("An edge needs a target node id.", nameof(to));
            }

            From = from;
            To = to;
        }

        public string From { get; }

        public string To { get; }

        public string Label
        {
            get { return _label; }
            set { _label = value ?? ""; }
        }

        /// <summary>How much travels along the edge, in the unit the area uses; 0 when the edge is a plain relation.</summary>
        public double Weight { get; set; }
    }
}
