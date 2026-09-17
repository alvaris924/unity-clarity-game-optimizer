using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// What one analyzer run over one scope found, in the one shape every area shares: metrics for the whole
    /// scope, findings with a verdict and evidence, a graph worth drawing, and notes the reader must see.
    /// Every number names its budget, and the exporters never put two size budgets in one table.
    /// </summary>
    internal sealed class Report
    {
        private string _unityVersion = "";
        private string _buildTarget = "";
        private string _packageVersion = "";

        public Report(Area area, string scope)
        {
            if (string.IsNullOrWhiteSpace(scope))
            {
                throw new ArgumentException("A report needs a scope, such as \"Project\" or \"Last build (Android)\".", nameof(scope));
            }

            Area = area;
            Scope = scope;
        }

        public Area Area { get; }

        /// <summary>What was looked at, in words: "Project", "Last build (Android, 2026-09-16)", "Prefab: Managers".</summary>
        public string Scope { get; }

        public string UnityVersion
        {
            get { return _unityVersion; }
            set { _unityVersion = value ?? ""; }
        }

        /// <summary>The active build target the numbers were measured for; empty when the area does not depend on one.</summary>
        public string BuildTarget
        {
            get { return _buildTarget; }
            set { _buildTarget = value ?? ""; }
        }

        public string PackageVersion
        {
            get { return _packageVersion; }
            set { _packageVersion = value ?? ""; }
        }

        /// <summary>When the scan ran, in UTC.</summary>
        public DateTime CreatedAt { get; set; }

        public List<Metric> Metrics { get; } = new List<Metric>();

        public List<Finding> Findings { get; } = new List<Finding>();

        public ReportGraph Graph { get; } = new ReportGraph();

        /// <summary>Caveats the reader must see, such as "the Editor reports font atlases at twice their device size".</summary>
        public List<string> Notes { get; } = new List<string>();

        public int CountFindings(Severity severity)
        {
            int count = 0;
            foreach (Finding finding in Findings)
            {
                if (finding.Severity == severity)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
