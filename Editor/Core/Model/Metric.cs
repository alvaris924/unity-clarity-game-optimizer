using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>One number that describes the whole scope, such as the total texture RAM or the assembly count.</summary>
    internal sealed class Metric
    {
        public Metric(string key, Measurement value, Budget budget, string label = null)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("A metric needs a key.", nameof(key));
            }

            Key = key;
            Value = value;
            Budget = budget;
            Label = string.IsNullOrEmpty(label) ? key : label;
        }

        /// <summary>Stable machine name, such as "textures.runtime-bytes"; unique within a report.</summary>
        public string Key { get; }

        /// <summary>The name shown to people; falls back to the key.</summary>
        public string Label { get; }

        public Measurement Value { get; }

        public Budget Budget { get; }
    }
}
