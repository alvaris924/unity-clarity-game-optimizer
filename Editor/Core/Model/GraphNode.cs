using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A thing in the report's graph: an assembly, a package, a prefab, a texture.</summary>
    internal sealed class GraphNode
    {
        private string _sublabel = "";
        private string _group = "";

        public GraphNode(string id, string label, NodeKind kind)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("A node needs an id.", nameof(id));
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                throw new ArgumentException("A node needs a label.", nameof(label));
            }

            Id = id;
            Label = label;
            Kind = kind;
        }

        /// <summary>Unique within the graph. Analyzers use the natural key: the assembly name, the asset path.</summary>
        public string Id { get; }

        public string Label { get; }

        public NodeKind Kind { get; }

        /// <summary>A second line under the label, such as "312 scripts" or "11.9 MB".</summary>
        public string Sublabel
        {
            get { return _sublabel; }
            set { _sublabel = value ?? ""; }
        }

        /// <summary>The boundary the node belongs to, such as "Runtime" or "Packages"; empty for none.</summary>
        public string Group
        {
            get { return _group; }
            set { _group = value ?? ""; }
        }

        /// <summary>The number the area ranks nodes by: fan-in, bytes, references. Curation keeps the heaviest.</summary>
        public double Weight { get; set; }

        public List<Evidence> Evidence { get; } = new List<Evidence>();
    }
}
