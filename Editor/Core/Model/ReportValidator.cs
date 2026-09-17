using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// The rules a report must satisfy before it is exported: unique keys and ids, edges that point at real
    /// nodes, a verdict on everything above Info, and a projection only next to a measurement in the same
    /// unit. Analyzers assert an empty result in their tests; exporters assume it.
    /// </summary>
    internal static class ReportValidator
    {
        public static List<string> Validate(Report report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var problems = new List<string>();

            if (report.CreatedAt == default(DateTime))
            {
                problems.Add("createdAt is not set");
            }

            var metricKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (Metric metric in report.Metrics)
            {
                if (!metricKeys.Add(metric.Key))
                {
                    problems.Add("metric '" + metric.Key + "' appears twice");
                }
            }

            var findingIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Finding finding in report.Findings)
            {
                if (!findingIds.Add(finding.Id))
                {
                    problems.Add("finding '" + finding.Id + "' appears twice");
                }

                if (finding.Title.Length == 0)
                {
                    problems.Add("finding '" + finding.Id + "' has no title");
                }

                if (finding.Severity > Severity.Info && finding.Verdict.Length == 0)
                {
                    problems.Add("finding '" + finding.Id + "' is " + finding.Severity + " but has no verdict");
                }

                if (finding.Projected.HasValue)
                {
                    if (!finding.Measured.HasValue)
                    {
                        problems.Add("finding '" + finding.Id + "' projects a value without measuring one");
                    }
                    else if (!string.Equals(finding.Measured.Value.Unit, finding.Projected.Value.Unit, StringComparison.Ordinal))
                    {
                        problems.Add("finding '" + finding.Id + "' measures in " + finding.Measured.Value.Unit + " but projects in " + finding.Projected.Value.Unit);
                    }
                }

                if (finding.Measured.HasValue && finding.Budget == Budget.None && finding.Measured.Value.Unit != Units.Count && finding.Measured.Value.Unit != Units.Percent)
                {
                    problems.Add("finding '" + finding.Id + "' measures " + finding.Measured.Value.Unit + " but names no budget");
                }
            }

            foreach (GraphEdge edge in report.Graph.Edges)
            {
                GraphNode ignored;
                if (!report.Graph.TryGetNode(edge.From, out ignored))
                {
                    problems.Add("edge " + edge.From + " -> " + edge.To + " starts at a node the graph does not have");
                }

                if (!report.Graph.TryGetNode(edge.To, out ignored))
                {
                    problems.Add("edge " + edge.From + " -> " + edge.To + " ends at a node the graph does not have");
                }
            }

            return problems;
        }
    }
}
