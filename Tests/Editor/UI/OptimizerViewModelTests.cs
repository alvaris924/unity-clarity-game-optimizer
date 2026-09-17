using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Tests.Core;
using ClarityGameOptimizer.UI;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.UI
{
    public class OptimizerViewModelTests
    {
        [Test]
        public void Starts_on_architecture_with_nothing_scanned()
        {
            var model = new OptimizerViewModel();

            Assert.That(model.SelectedArea, Is.EqualTo(Area.Architecture));
            Assert.That(model.Current, Is.Null);
            Assert.That(model.Findings, Is.Empty);
            Assert.That(model.HeadlineMetrics, Is.Empty);
            Assert.That(model.FindingCount(Area.Architecture), Is.Null);
            Assert.That(model.ScanSummary, Is.EqualTo("Press Scan to read the project."));
        }

        [Test]
        public void A_stored_report_drives_findings_cards_counts_and_the_summary()
        {
            var model = new OptimizerViewModel();
            int changed = 0;
            model.Changed += () => changed++;

            model.Store(ArchitectureFixtures.Report(), TimeSpan.FromMilliseconds(420));

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(model.Current, Is.Not.Null);
            Assert.That(model.Findings.Select(finding => finding.Id), Is.EqualTo(new[]
            {
                "assembly.outside-definition:Assembly-CSharp",
                "assembly.large:Game.Runtime",
                "assembly.outside-definition:Assembly-CSharp-Editor",
                "assembly.hub:Game.Events",
            }), "most severe first, then the largest measurement");
            Assert.That(model.HeadlineMetrics.Select(metric => metric.Key), Is.EqualTo(new[] { "assemblies.count", "assemblies.project", "scripts.outside-definition-share", "assemblies.max-fan-in" }));
            Assert.That(model.FindingCount(Area.Architecture), Is.EqualTo(4));
            Assert.That(model.FindingCount(Area.Memory), Is.Null);
            Assert.That(model.ScanSummary, Is.EqualTo("Scanned Project in 0.4 s: 10 nodes, 4 findings (0 critical, 1 warnings, 1 advice, 2 info)."));
        }

        [Test]
        public void Selecting_a_planned_area_says_so_and_selecting_again_is_silent()
        {
            var model = new OptimizerViewModel();
            int changed = 0;
            model.Changed += () => changed++;

            model.SelectedArea = Area.Performance;
            model.SelectedArea = Area.Performance;

            Assert.That(changed, Is.EqualTo(1));
            Assert.That(model.Current, Is.Null);
            Assert.That(model.ScanSummary, Is.EqualTo("Performance is not here yet: planned for v1.0.0."));
        }

        [Test]
        public void Reports_are_kept_per_area_and_the_headline_falls_back_to_the_first_four_metrics()
        {
            var model = new OptimizerViewModel();
            var memory = new Report(Area.Memory, "Last build") { CreatedAt = ReportFixtures.Timestamp };
            for (int i = 0; i < 6; i++)
            {
                memory.Metrics.Add(new Metric("m" + i, Measurement.Bytes(i), Budget.RuntimeRam));
            }

            model.Store(ArchitectureFixtures.Report(), TimeSpan.Zero);
            model.Store(memory, TimeSpan.FromSeconds(2));
            model.SelectedArea = Area.Memory;

            Assert.That(model.Current, Is.SameAs(memory));
            Assert.That(model.HeadlineMetrics.Select(metric => metric.Key), Is.EqualTo(new[] { "m0", "m1", "m2", "m3" }), "the Memory descriptor names no headline metrics yet");
            Assert.That(model.ReportFor(Area.Architecture), Is.Not.Null);
            Assert.That(model.ScanSummary, Does.StartWith("Scanned Last build in 2.0 s: 0 nodes, 0 findings"));
        }

        [Test]
        public void Rejects_a_null_report()
        {
            Assert.That(() => new OptimizerViewModel().Store(null, TimeSpan.Zero), Throws.ArgumentNullException);
        }
    }
}
