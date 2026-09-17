using System.Collections.Generic;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class AssemblyGraphReportTests
    {
        [Test]
        public void The_fixture_yields_a_valid_architecture_report()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Assert.That(report.Area, Is.EqualTo(Area.Architecture));
            Assert.That(report.Scope, Is.EqualTo("Project"));
            report.CreatedAt = ReportFixtures.Timestamp;
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Nodes_are_sorted_by_name_and_typed_and_grouped_by_role()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Assert.That(report.Graph.Nodes.Select(node => node.Id), Is.EqualTo(new[]
            {
                "Assembly-CSharp", "Assembly-CSharp-Editor", "Game.Editor", "Game.Events", "Game.Runtime", "Game.Tests",
                "PlayFab.Wrappers", "UniTask", "Unity.TextMeshPro", "UnityEngine.TestRunner",
            }));
            Assert.That(Node(report, "Game.Runtime").Kind, Is.EqualTo(NodeKind.Runtime));
            Assert.That(Node(report, "Game.Runtime").Group, Is.EqualTo("Runtime"));
            Assert.That(Node(report, "Game.Editor").Kind, Is.EqualTo(NodeKind.Editor));
            Assert.That(Node(report, "Game.Editor").Group, Is.EqualTo("Editor"));
            Assert.That(Node(report, "Game.Tests").Kind, Is.EqualTo(NodeKind.Test));
            Assert.That(Node(report, "Game.Tests").Group, Is.EqualTo("Tests"));
            Assert.That(Node(report, "Unity.TextMeshPro").Kind, Is.EqualTo(NodeKind.External));
            Assert.That(Node(report, "Unity.TextMeshPro").Group, Is.EqualTo("Packages"));
            Assert.That(Node(report, "UnityEngine.TestRunner").Kind, Is.EqualTo(NodeKind.External), "a package assembly is external even when it is the test framework");
            Assert.That(Node(report, "UniTask").Kind, Is.EqualTo(NodeKind.External), "third-party code under Assets/Plugins is external");
            Assert.That(Node(report, "UniTask").Group, Is.EqualTo("Plugins"));
            Assert.That(Node(report, "Assembly-CSharp-Editor").Kind, Is.EqualTo(NodeKind.Editor));
        }

        [Test]
        public void Nodes_carry_the_asmdef_as_evidence_when_there_is_one()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Assert.That(Node(report, "Game.Runtime").Evidence.Single().Path, Is.EqualTo("Assets/Game/Game.Runtime.asmdef"));
            Assert.That(Node(report, "Game.Runtime").Evidence.Single().Label, Is.EqualTo("asmdef"));
            Assert.That(Node(report, "Assembly-CSharp").Evidence, Is.Empty);
        }

        [Test]
        public void Edges_follow_references_and_weights_combine_project_fan_in_with_size()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Assert.That(report.Graph.Edges.Any(edge => edge.From == "Game.Editor" && edge.To == "Game.Runtime" && edge.Label == "references"));
            Assert.That(Node(report, "Game.Events").Weight, Is.EqualTo(5.6).Within(0.001), "referenced by Runtime, Editor, Tests, Assembly-CSharp and PlayFab.Wrappers, plus 30 scripts");
            Assert.That(Node(report, "UniTask").Weight, Is.EqualTo(6.52).Within(0.001), "referenced by the same five, plus 76 scripts");
            Assert.That(Node(report, "Game.Runtime").Weight, Is.EqualTo(9.24).Within(0.001), "referenced by Editor, Tests and Assembly-CSharp, plus 312 scripts");
            Assert.That(Node(report, "Assembly-CSharp").Weight, Is.EqualTo(11).Within(0.001), "referenced by Assembly-CSharp-Editor, as in Unity, plus 500 scripts");
            Assert.That(Node(report, "Assembly-CSharp-Editor").Weight, Is.EqualTo(0.4).Within(0.001), "nothing references it; 20 scripts");
            Assert.That(Node(report, "Game.Events").Sublabel, Is.EqualTo("30 scripts · used by 5"));
        }

        [Test]
        public void Only_the_projects_own_assemblies_count_toward_fan_in()
        {
            List<AssemblyDescription> assemblies = Fixture();
            AssemblyDescription runner = assemblies.Single(assembly => assembly.Name == "UnityEngine.TestRunner");
            runner.References.Add("Unity.TextMeshPro");
            AssemblyDescription plugin = assemblies.Single(assembly => assembly.Name == "UniTask");
            plugin.References.Add("Unity.TextMeshPro");

            Report report = AssemblyGraphReport.Build(assemblies);

            Assert.That(report.Graph.Edges.Count(edge => edge.To == "Unity.TextMeshPro"), Is.EqualTo(4), "the edges are drawn");
            Assert.That(Node(report, "Unity.TextMeshPro").Weight, Is.EqualTo(3.8).Within(0.001), "but only Game.Runtime and Assembly-CSharp weigh, plus 90 scripts");
            Assert.That(report.Metrics.Single(metric => metric.Key == "assemblies.max-fan-in").Value.Value, Is.EqualTo(5), "the metric stays plain fan-in");
        }

        [Test]
        public void Edges_are_sorted_deduplicated_and_never_self_referencing_or_dangling()
        {
            List<AssemblyDescription> assemblies = Fixture();
            AssemblyDescription runtime = assemblies.Single(assembly => assembly.Name == "Game.Runtime");
            runtime.References.Add("Game.Events");
            runtime.References.Add("Game.Runtime");
            runtime.References.Add("Ghost");

            Report report = AssemblyGraphReport.Build(assemblies);

            GraphEdge[] fromRuntime = report.Graph.Edges.Where(edge => edge.From == "Game.Runtime").ToArray();
            Assert.That(fromRuntime.Select(edge => edge.To), Is.EqualTo(new[] { "Game.Events", "PlayFab.Wrappers", "UniTask", "Unity.TextMeshPro" }));
            Assert.That(report.Notes, Has.Some.EqualTo("1 reference to assemblies outside the scan was ignored."));
            report.CreatedAt = ReportFixtures.Timestamp;
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Metrics_count_assemblies_scripts_and_the_share_outside_any_definition()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Assert.That(Metric(report, "assemblies.count").Value.Value, Is.EqualTo(10));
            Assert.That(Metric(report, "assemblies.project").Value.Value, Is.EqualTo(7));
            Assert.That(Metric(report, "assemblies.plugins").Value.Value, Is.EqualTo(1));
            Assert.That(Metric(report, "assemblies.packages").Value.Value, Is.EqualTo(2));
            Assert.That(Metric(report, "scripts.project").Value.Value, Is.EqualTo(928), "plugin scripts are not the project's own");
            Assert.That(Metric(report, "scripts.outside-definition").Value.Value, Is.EqualTo(520));
            Assert.That(Metric(report, "scripts.outside-definition-share").Value.Value, Is.EqualTo(100.0 * 520 / 928).Within(0.0001));
            Assert.That(Metric(report, "scripts.outside-definition-share").Value.Unit, Is.EqualTo(Units.Percent));
            Assert.That(Metric(report, "references.count").Value.Value, Is.EqualTo(report.Graph.Edges.Count));
            Assert.That(Metric(report, "assemblies.max-fan-in").Value.Value, Is.EqualTo(5));
        }

        [Test]
        public void Scripts_outside_any_definition_are_graded_by_their_share_of_the_project()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Finding big = Finding(report, AssemblyGraphReport.OutsideDefinitionCheck, "Assembly-CSharp");
            Assert.That(big.Severity, Is.EqualTo(Severity.Warning), "500 of 928 scripts is more than half");
            Assert.That(big.Title, Is.EqualTo("500 scripts compile outside any assembly definition (53.9 % of the project)"));
            Assert.That(big.Measured.Value.Value, Is.EqualTo(500));
            Assert.That(big.Verdict, Does.StartWith("Move them into assembly definitions"));

            Finding small = Finding(report, AssemblyGraphReport.OutsideDefinitionCheck, "Assembly-CSharp-Editor");
            Assert.That(small.Severity, Is.EqualTo(Severity.Info), "20 of 928 scripts is under a tenth");
            Assert.That(small.Verdict, Is.EqualTo(""));
        }

        [Test]
        public void Hubs_and_large_assemblies_are_reported_with_their_asmdef()
        {
            Report report = AssemblyGraphReport.Build(Fixture());

            Finding hub = Finding(report, AssemblyGraphReport.HubCheck, "Game.Events");
            Assert.That(hub.Severity, Is.EqualTo(Severity.Info));
            Assert.That(hub.Title, Is.EqualTo("Referenced by 5 project assemblies; a change here recompiles all of them"));
            Assert.That(hub.Evidence.Single().Path, Is.EqualTo("Assets/Game/Events/Game.Events.asmdef"));

            Finding large = Finding(report, AssemblyGraphReport.LargeCheck, "Game.Runtime");
            Assert.That(large.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(large.Title, Is.EqualTo("312 scripts recompile on any change inside this assembly"));
            Assert.That(large.Verdict, Does.StartWith("Split it"));

            Assert.That(report.Findings.Select(finding => finding.Id), Is.EqualTo(new[]
            {
                "assembly.outside-definition:Assembly-CSharp",
                "assembly.outside-definition:Assembly-CSharp-Editor",
                "assembly.hub:Game.Events",
                "assembly.large:Game.Runtime",
            }), "one finding per check per project assembly, in name order; plugins and packages get none even as hubs");
        }

        [Test]
        public void The_thresholds_come_from_the_rules()
        {
            Report strict = AssemblyGraphReport.Build(Fixture(), new ArchitectureRules(6, 400));
            Report loose = AssemblyGraphReport.Build(Fixture(), new ArchitectureRules(3, 20));

            Assert.That(strict.Findings.Select(finding => finding.Check), Has.None.EqualTo(AssemblyGraphReport.HubCheck), "Game.Events has five referrers, under six");
            Assert.That(strict.Findings.Select(finding => finding.Check), Has.None.EqualTo(AssemblyGraphReport.LargeCheck), "Game.Runtime has 312 scripts, under 400");
            Assert.That(loose.Findings.Count(finding => finding.Check == AssemblyGraphReport.HubCheck), Is.EqualTo(2), "Game.Events and Game.Runtime");
            Assert.That(loose.Findings.Count(finding => finding.Check == AssemblyGraphReport.LargeCheck), Is.EqualTo(3), "Game.Runtime, Game.Editor and Game.Events have 20 scripts or more; PlayFab.Wrappers and Game.Tests do not");
            Assert.That(ArchitectureRules.Default.HubFanIn, Is.EqualTo(5));
            Assert.That(ArchitectureRules.Default.LargeSourceFiles, Is.EqualTo(250));
            Assert.That(() => new ArchitectureRules(0, 1), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void An_empty_project_yields_zero_metrics_and_no_findings()
        {
            Report report = AssemblyGraphReport.Build(new List<AssemblyDescription>());

            Assert.That(report.Graph.IsEmpty);
            Assert.That(report.Findings, Is.Empty);
            Assert.That(Metric(report, "scripts.outside-definition-share").Value.Value, Is.EqualTo(0));
            report.CreatedAt = ReportFixtures.Timestamp;
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void An_outside_definition_assembly_without_scripts_yields_no_finding()
        {
            var assemblies = new List<AssemblyDescription> { new AssemblyDescription("Assembly-CSharp") };

            Report report = AssemblyGraphReport.Build(assemblies);

            Assert.That(report.Findings, Is.Empty);
        }

        [Test]
        public void The_same_input_in_another_order_yields_the_same_report()
        {
            List<AssemblyDescription> forward = Fixture();
            List<AssemblyDescription> backward = Fixture();
            backward.Reverse();

            string first = ReportJsonExporter.Write(Stamped(AssemblyGraphReport.Build(forward)));
            string second = ReportJsonExporter.Write(Stamped(AssemblyGraphReport.Build(backward)));

            Assert.That(second, Is.EqualTo(first));
        }

        private static Report Stamped(Report report)
        {
            report.CreatedAt = ReportFixtures.Timestamp;
            return report;
        }

        private static GraphNode Node(Report report, string id)
        {
            GraphNode node;
            Assert.That(report.Graph.TryGetNode(id, out node), Is.True, id);
            return node;
        }

        private static Metric Metric(Report report, string key)
        {
            return report.Metrics.Single(metric => metric.Key == key);
        }

        private static Finding Finding(Report report, string check, string subject)
        {
            return report.Findings.Single(finding => finding.Check == check && finding.Subject == subject);
        }

        private static List<AssemblyDescription> Fixture()
        {
            return ArchitectureFixtures.TenAssemblies();
        }
    }
}
