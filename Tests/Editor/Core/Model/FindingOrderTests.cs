using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class FindingOrderTests
    {
        [Test]
        public void Orders_by_severity_then_measurement_then_subject()
        {
            Report report = ReportFixtures.MixedBudgets();

            Assert.That(FindingOrder.MostSevereFirst(report.Findings).Select(finding => finding.Subject), Is.EqualTo(new[]
            {
                "Assets/UI/Loading.png", "Assets/Atlas_01.png", "Assets/Atlas_02.png", "Assets/Audio/Music.ogg", "Last build",
            }), "the Critical first, then the two equal Warnings by subject, then Advice, then Info");
        }

        [Test]
        public void Unmeasured_findings_come_after_measured_ones_of_the_same_severity()
        {
            var measured = new Finding("check", "b") { Severity = Severity.Info, Measured = Measurement.Count(1) };
            var unmeasured = new Finding("check", "a") { Severity = Severity.Info };

            Assert.That(FindingOrder.MostSevereFirst(new[] { unmeasured, measured }), Is.EqualTo(new[] { measured, unmeasured }));
            Assert.That(() => FindingOrder.MostSevereFirst(null), Throws.ArgumentNullException);
        }
    }
}
