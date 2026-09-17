using System;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class ReportModelTests
    {
        [Test]
        public void A_finding_id_is_the_check_and_the_subject()
        {
            var finding = new Finding("texture.max-size", "Assets/Atlas_01.png");

            Assert.That(finding.Id, Is.EqualTo("texture.max-size:Assets/Atlas_01.png"));
        }

        [Test]
        public void A_finding_needs_a_check_and_a_subject()
        {
            Assert.That(() => new Finding("", "subject"), Throws.ArgumentException);
            Assert.That(() => new Finding("check", " "), Throws.ArgumentException);
        }

        [Test]
        public void Finding_text_never_becomes_null()
        {
            var finding = new Finding("check", "subject") { Title = null, Verdict = null };

            Assert.That(finding.Title, Is.EqualTo(""));
            Assert.That(finding.Verdict, Is.EqualTo(""));
        }

        [Test]
        public void A_report_needs_a_scope_and_keeps_its_strings_non_null()
        {
            Assert.That(() => new Report(Area.Memory, ""), Throws.ArgumentException);

            var report = new Report(Area.Memory, "Project") { UnityVersion = null, BuildTarget = null, PackageVersion = null };
            Assert.That(report.UnityVersion, Is.EqualTo(""));
            Assert.That(report.BuildTarget, Is.EqualTo(""));
            Assert.That(report.PackageVersion, Is.EqualTo(""));
        }

        [Test]
        public void A_report_counts_findings_by_severity()
        {
            Report report = ReportFixtures.MixedBudgets();

            Assert.That(report.CountFindings(Severity.Warning), Is.EqualTo(2));
            Assert.That(report.CountFindings(Severity.Critical), Is.EqualTo(1));
        }

        [Test]
        public void Evidence_locators_follow_the_editor_convention()
        {
            Assert.That(new Evidence("Assets/A.cs").Locator, Is.EqualTo("Assets/A.cs"));
            Assert.That(new Evidence("Assets/A.cs", 12).Locator, Is.EqualTo("Assets/A.cs:12"));
            Assert.That(new Evidence("Assets/A.cs", 12, 20).Locator, Is.EqualTo("Assets/A.cs:12-20"));
        }

        [Test]
        public void Evidence_rejects_an_empty_path_and_an_inverted_range()
        {
            Assert.That(() => new Evidence(" "), Throws.ArgumentException);
            Assert.That(() => new Evidence("Assets/A.cs", -1), Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(() => new Evidence("Assets/A.cs", 20, 12), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void A_metric_label_falls_back_to_its_key()
        {
            Assert.That(new Metric("textures.bytes", Measurement.Bytes(1), Budget.RuntimeRam).Label, Is.EqualTo("textures.bytes"));
            Assert.That(new Metric("textures.bytes", Measurement.Bytes(1), Budget.RuntimeRam, "Texture RAM").Label, Is.EqualTo("Texture RAM"));
        }

        [Test]
        public void A_graph_keeps_insertion_order_and_rejects_duplicate_node_ids()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("b", "B", NodeKind.Runtime));
            graph.AddNode(new GraphNode("a", "A", NodeKind.Editor));

            Assert.That(graph.Nodes[0].Id, Is.EqualTo("b"));
            Assert.That(graph.Nodes[1].Id, Is.EqualTo("a"));
            Assert.That(() => graph.AddNode(new GraphNode("a", "again", NodeKind.Runtime)), Throws.InvalidOperationException);
        }

        [Test]
        public void A_graph_finds_nodes_by_id()
        {
            var graph = new ReportGraph();
            GraphNode added = graph.AddNode(new GraphNode("a", "A", NodeKind.Runtime));

            GraphNode found;
            Assert.That(graph.TryGetNode("a", out found), Is.True);
            Assert.That(found, Is.SameAs(added));
            Assert.That(graph.TryGetNode("missing", out found), Is.False);
            Assert.That(graph.TryGetNode(null, out found), Is.False);
            Assert.That(graph.IsEmpty, Is.False);
        }

        [Test]
        public void Nodes_and_edges_need_ids_and_keep_text_non_null()
        {
            Assert.That(() => new GraphNode("", "A", NodeKind.Runtime), Throws.ArgumentException);
            Assert.That(() => new GraphNode("a", "", NodeKind.Runtime), Throws.ArgumentException);
            Assert.That(() => new GraphEdge("", "b"), Throws.ArgumentException);
            Assert.That(() => new GraphEdge("a", ""), Throws.ArgumentException);

            var node = new GraphNode("a", "A", NodeKind.Runtime) { Sublabel = null, Group = null };
            Assert.That(node.Sublabel, Is.EqualTo(""));
            Assert.That(node.Group, Is.EqualTo(""));
            Assert.That(new GraphEdge("a", "b") { Label = null }.Label, Is.EqualTo(""));
        }

        [Test]
        public void Area_and_budget_names_read_as_words()
        {
            Assert.That(Areas.Describe(Area.BuildSize), Is.EqualTo("Build Size"));
            Assert.That(Areas.Describe(Area.CodeQuality), Is.EqualTo("Code Quality"));
            Assert.That(Areas.Describe(Area.Memory), Is.EqualTo("Memory"));
            Assert.That(Budgets.Describe(Budget.RuntimeRam), Is.EqualTo("Runtime RAM"));
            Assert.That(Budgets.Describe(Budget.Download), Is.EqualTo("Download size"));
            Assert.That(Budgets.Describe(Budget.None), Is.EqualTo("None"));
        }
    }
}
