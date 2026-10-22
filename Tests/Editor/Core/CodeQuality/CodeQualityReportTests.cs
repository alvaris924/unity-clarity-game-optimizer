using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class CodeQualityReportTests
    {
        private static Report Build(params SourceFileDescription[] files)
        {
            Report report = CodeQualityReport.Build(files);
            report.CreatedAt = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(ReportValidator.Validate(report), Is.Empty);
            return report;
        }

        [Test]
        public void One_row_per_pattern_per_method_with_the_count_and_lines()
        {
            const string source = @"public class Bullet : MonoBehaviour
{
    void Update()
    {
        var a = GetComponent<Rigidbody>();
        var b = GetComponent<Collider>();
        Debug.Log(""moving"");
    }

    void LateUpdate()
    {
        var c = GetComponent<Renderer>();
    }
}";
            Report report = Build(new SourceFileDescription("Assets/Scripts/Bullet.cs", source));

            Finding[] lookups = report.Findings.Where(f => f.Check == "code.per-frame.get-component").ToArray();
            Assert.That(lookups.Select(f => f.Subject), Is.EquivalentTo(new[] { "Assets/Scripts/Bullet.cs:3", "Assets/Scripts/Bullet.cs:10" }));
            Finding update = lookups.Single(f => f.Subject.EndsWith(":3", StringComparison.Ordinal));
            Assert.That(update.Measured.Value.Value, Is.EqualTo(2));
            Assert.That(update.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(update.Budget, Is.EqualTo(Budget.FrameTime));
            Assert.That(update.Title, Is.EqualTo("Update() looks up components 2 times every frame"));
            Assert.That(update.Evidence.Select(e => e.Line), Is.EqualTo(new[] { 5, 6 }));
            Assert.That(report.Findings.Single(f => f.Check == "code.per-frame.log").Severity, Is.EqualTo(Severity.Advice));
            Assert.That(report.Metrics.Single(m => m.Key == "hits.per-frame").Value.Value, Is.EqualTo(4));
            Assert.That(report.Metrics.Single(m => m.Key == "methods.per-frame").Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Empty_messages_and_maskless_queries_get_their_own_rows()
        {
            Report report = Build(
                new SourceFileDescription("Assets/A.cs", "class A { void Update() { } }"),
                new SourceFileDescription("Assets/B.cs", "class B { void Hit() { var h = Physics.OverlapSphere(p, r); var g = Physics.OverlapSphere(p, r); } }"));

            Assert.That(report.Findings.Single(f => f.Check == CodeQualityReport.EmptyMessageCheck).Subject, Is.EqualTo("Assets/A.cs:1"));
            Assert.That(report.Findings.Count(f => f.Check == CodeQualityReport.NoMaskCheck), Is.EqualTo(1));
            Assert.That(report.Metrics.Single(m => m.Key == "scripts.scanned").Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Camera_main_is_information_only()
        {
            Report report = Build(new SourceFileDescription("Assets/C.cs", "class C { void Update() { var cam = Camera.main; } }"));
            Assert.That(report.Findings.Single().Severity, Is.EqualTo(Severity.Info));
        }

        [Test]
        public void Every_pattern_has_a_check_name()
        {
            foreach (CodePattern pattern in Enum.GetValues(typeof(CodePattern)).Cast<CodePattern>())
            {
                Assert.That(CodeQualityReport.CheckOf(pattern), Does.StartWith("code."), pattern.ToString());
            }

            Assert.That(Enum.GetValues(typeof(CodePattern)).Cast<CodePattern>().Select(CodeQualityReport.CheckOf).Distinct().Count(), Is.EqualTo(Enum.GetValues(typeof(CodePattern)).Length));
        }
    }
}
