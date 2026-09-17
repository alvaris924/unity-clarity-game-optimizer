using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class ReportValidatorTests
    {
        [Test]
        public void Accepts_the_fixtures()
        {
            Assert.That(ReportValidator.Validate(ReportFixtures.Architecture()), Is.Empty);
            Assert.That(ReportValidator.Validate(ReportFixtures.MixedBudgets()), Is.Empty);
        }

        [Test]
        public void Requires_a_timestamp()
        {
            var report = new Report(Area.Assets, "Project");

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "createdAt is not set" }));
        }

        [Test]
        public void Reports_a_duplicate_metric_key()
        {
            Report report = Stamped(Area.Memory);
            report.Metrics.Add(new Metric("bytes", Measurement.Bytes(1), Budget.RuntimeRam));
            report.Metrics.Add(new Metric("bytes", Measurement.Bytes(2), Budget.RuntimeRam));

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "metric 'bytes' appears twice" }));
        }

        [Test]
        public void Reports_a_duplicate_finding()
        {
            Report report = Stamped(Area.Memory);
            report.Findings.Add(Valid("check", "subject"));
            report.Findings.Add(Valid("check", "subject"));

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "finding 'check:subject' appears twice" }));
        }

        [Test]
        public void Requires_a_title()
        {
            Report report = Stamped(Area.Memory);
            report.Findings.Add(new Finding("check", "subject") { Severity = Severity.Info });

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "finding 'check:subject' has no title" }));
        }

        [Test]
        public void Requires_a_verdict_above_info()
        {
            Report report = Stamped(Area.Memory);
            report.Findings.Add(new Finding("check", "quiet") { Title = "t", Severity = Severity.Info });
            report.Findings.Add(new Finding("check", "loud") { Title = "t", Severity = Severity.Warning });

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "finding 'check:loud' is Warning but has no verdict" }));
        }

        [Test]
        public void Requires_a_measurement_under_a_projection_in_the_same_unit()
        {
            Report report = Stamped(Area.Memory);
            Finding orphan = Valid("check", "orphan");
            orphan.Projected = Measurement.Bytes(1);
            report.Findings.Add(orphan);
            Finding mismatch = Valid("check", "mismatch");
            mismatch.Budget = Budget.RuntimeRam;
            mismatch.Measured = Measurement.Bytes(2);
            mismatch.Projected = Measurement.Milliseconds(1);
            report.Findings.Add(mismatch);

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[]
            {
                "finding 'check:orphan' projects a value without measuring one",
                "finding 'check:mismatch' measures in bytes but projects in ms",
            }));
        }

        [Test]
        public void Requires_a_budget_for_sizes_and_times_but_not_for_counts()
        {
            Report report = Stamped(Area.Memory);
            Finding sized = Valid("check", "sized");
            sized.Measured = Measurement.Bytes(1);
            report.Findings.Add(sized);
            Finding counted = Valid("check", "counted");
            counted.Measured = Measurement.Count(1);
            report.Findings.Add(counted);

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[] { "finding 'check:sized' measures bytes but names no budget" }));
        }

        [Test]
        public void Reports_edges_that_point_at_missing_nodes()
        {
            Report report = Stamped(Area.Architecture);
            report.Graph.AddNode(new GraphNode("a", "A", NodeKind.Runtime));
            report.Graph.AddEdge(new GraphEdge("a", "b"));
            report.Graph.AddEdge(new GraphEdge("c", "a"));

            Assert.That(ReportValidator.Validate(report), Is.EqualTo(new[]
            {
                "edge a -> b ends at a node the graph does not have",
                "edge c -> a starts at a node the graph does not have",
            }));
        }

        [Test]
        public void Rejects_a_null_report()
        {
            Assert.That(() => ReportValidator.Validate(null), Throws.ArgumentNullException);
        }

        private static Report Stamped(Area area)
        {
            return new Report(area, "Project") { CreatedAt = ReportFixtures.Timestamp };
        }

        private static Finding Valid(string check, string subject)
        {
            return new Finding(check, subject) { Title = "title", Severity = Severity.Warning, Verdict = "verdict" };
        }
    }
}
