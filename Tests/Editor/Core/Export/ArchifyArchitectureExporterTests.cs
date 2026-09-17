using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class ArchifyArchitectureExporterTests
    {
        [Test]
        public void Matches_the_committed_fixtures()
        {
            foreach (KeyValuePair<string, string> fixture in DiagramFixtures.Expected())
            {
                string path = DiagramFixtures.PathOf(fixture.Key);
                Assert.That(File.Exists(path), Is.True, path + " is missing; run DiagramFixtures.Update");
                string committed = File.ReadAllText(path).Replace("\r\n", "\n");
                if (committed != fixture.Value)
                {
                    string actual = Path.Combine(Path.GetTempPath(), fixture.Key);
                    File.WriteAllText(actual, fixture.Value);
                    Assert.Fail(fixture.Key + " differs from the exporter's output; the output was written to " + actual + ". Run DiagramFixtures.Update after an intentional change.");
                }
            }
        }

        [Test]
        public void Writes_the_archify_envelope_with_the_unity_legend()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report());

            Assert.That(json, Does.StartWith("{\n  \"schema_version\": 1,\n  \"diagram_type\": \"architecture\",\n  \"meta\": {\n    \"title\": \"Assembly map\",\n"));
            Assert.That(json, Does.Contain("\"subtitle\": \"10 assemblies · Clarity Game Optimizer 0.1.0 · 2026-09-17\""));
            Assert.That(json, Does.Contain("\"quality_profile\": \"standard\""));
            Assert.That(json, Does.Contain("\"legend\": {\n      \"mode\": \"auto\",\n      \"entries\": {\n        \"backend\": {\n          \"label\": \"Runtime code\"\n        },"));
            Assert.That(json, Does.Contain("\"external\": {\n          \"label\": \"Packages and plugins\"\n        }"));
            Assert.That(json, Does.Contain("\"layout\": {\n    \"mode\": \"grid\","));
        }

        [Test]
        public void Places_every_node_on_its_own_row_in_column_order_with_its_type()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report());

            MatchCollection cells = Regex.Matches(json, "\"row\": (\\d+),\\s*\"col\": (\\d+)");
            Assert.That(cells.Count, Is.EqualTo(10), "one cell per component");
            int[] rows = cells.Cast<Match>().Select(match => int.Parse(match.Groups[1].Value)).OrderBy(row => row).ToArray();
            Assert.That(rows, Is.EqualTo(Enumerable.Range(0, 10)), "rows 0 to 9, each used once");
            int[] columnsByRow = cells.Cast<Match>().OrderBy(match => int.Parse(match.Groups[1].Value)).Select(match => int.Parse(match.Groups[2].Value)).ToArray();
            Assert.That(columnsByRow, Is.Ordered, "walking down the rows never goes back a column");
            Assert.That(json, Does.Contain("\"cellW\": 178"), "22 characters in the longest label at seven pixels each plus padding");
            Assert.That(json, Does.Contain("\"size\": [\n        178,\n        64\n      ]"));
            Assert.That(json, Does.Contain("\"id\": \"Game-Runtime\",\n      \"type\": \"backend\",\n      \"label\": \"Game.Runtime\",\n      \"sublabel\": \"312 scripts · used by 3\",\n      \"tag\": \"Runtime\",\n      \"row\":"));
            Assert.That(json, Does.Contain("\"id\": \"Game-Tests\",\n      \"type\": \"security\","));
            Assert.That(json, Does.Contain("\"id\": \"Game-Editor\",\n      \"type\": \"frontend\","));
            Assert.That(json, Does.Contain("\"id\": \"UniTask\",\n      \"type\": \"external\","));
            Assert.That(json, Does.Not.Contain("\"sources\""), "no sources without a public repository to verify them against");
            Assert.That(json, Does.Not.Contain("\"repository\""));
        }

        [Test]
        public void Writes_sources_and_the_repository_when_a_public_repository_is_named()
        {
            var repository = new ArchifyRepository("https://github.com/alvaris924/example", "C826E6C3A7ABAD19C0F3CD1CA57207D54B1AD8DE");

            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report(), 12, "Assembly map", repository);

            Assert.That(json, Does.Contain("\"repository\": {\n      \"url\": \"https://github.com/alvaris924/example\",\n      \"revision\": \"c826e6c3a7abad19c0f3cd1ca57207d54b1ad8de\"\n    },"));
            Assert.That(json, Does.Contain("\"label\": \"Game.Runtime\",\n      \"sublabel\": \"312 scripts · used by 3\",\n      \"tag\": \"Runtime\",\n      \"sources\": [\n        {\n          \"path\": \"Assets/Game/Game.Runtime.asmdef\",\n          \"label\": \"asmdef\"\n        }\n      ],"));
            Assert.That(json, Does.Contain("\"label\": \"Assembly-CSharp\",\n      \"sublabel\": \"500 scripts · used by 1\",\n      \"tag\": \"Runtime\",\n      \"row\":"), "no sources without an asmdef");
        }

        [Test]
        public void A_repository_must_be_public_github_at_a_full_revision()
        {
            Assert.That(() => new ArchifyRepository("https://gitlab.com/a/b", new string('a', 40)), Throws.ArgumentException);
            Assert.That(() => new ArchifyRepository("https://github.com/a/b", "abc123"), Throws.ArgumentException);
            Assert.That(new ArchifyRepository("https://github.com/a/b.git", new string('A', 40)).Revision, Is.EqualTo(new string('a', 40)));
        }

        [Test]
        public void Tags_each_component_with_its_group_and_offers_a_view_per_group()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report());

            Assert.That(json, Does.Contain("\"label\": \"UniTask\",\n      \"sublabel\": \"76 scripts · used by 5\",\n      \"tag\": \"Plugins\","));
            Assert.That(json, Does.Contain("\"label\": \"Unity.TextMeshPro\",\n      \"sublabel\": \"90 scripts · used by 2\",\n      \"tag\": \"Packages\","));
            Assert.That(json, Does.Not.Contain("\"boundaries\""), "regions would sprawl across the staircase and collide with labels");
            Assert.That(json, Does.Contain("\"views\": [\n      {\n        \"id\": \"view-Runtime\",\n        \"label\": \"Runtime\",\n        \"focus\": [\n          \"Assembly-CSharp\",\n          \"Game-Events\",\n          \"Game-Runtime\",\n          \"PlayFab-Wrappers\"\n        ]\n      },"));
            Assert.That(Regex.Matches(json, "\"id\": \"view-").Count, Is.EqualTo(5), "Runtime, Editor, Tests, Plugins, Packages");
        }

        [Test]
        public void Connections_grow_with_the_references_they_stand_for_and_folded_endpoints_are_dashed()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report(), 4);

            Assert.That(json, Does.Not.Contain("\"label\": \"references\""), "connections carry no labels at this density");
            Assert.That(json, Does.Contain("\"from\": \"Game-Runtime\",\n      \"to\": \"Game-Events\"\n    }"), "a single reference is a plain line");
            Assert.That(json, Does.Contain("\"from\": \"Game-Runtime\",\n      \"to\": \"other-Runtime\",\n      \"variant\": \"dashed\""), "a folded endpoint dashes the line");
            Assert.That(json, Does.Contain("\"subtitle\": \"10 assemblies, 8 drawn, 6 folded"), "three project nodes, one external, one Other per remaining group");

            string two = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report(), 2);
            Assert.That(two, Does.Contain("\"from\": \"Game-Runtime\",\n      \"to\": \"other-Runtime\",\n      \"width\": 1.8,\n      \"variant\": \"dashed\""), "Game.Events and PlayFab.Wrappers fold together, so the two references from Game.Runtime become one heavier dashed line");
            Assert.That(ArchifyArchitectureExporter.LineWidth(2), Is.EqualTo(1.8));
            Assert.That(ArchifyArchitectureExporter.LineWidth(349), Is.EqualTo(6), "capped");
        }

        [Test]
        public void The_projects_own_assemblies_are_kept_before_the_packages_they_use()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report(), 4);

            Assert.That(json, Does.Contain("\"label\": \"Game.Events\""));
            Assert.That(json, Does.Contain("\"label\": \"Game.Runtime\""));
            Assert.That(json, Does.Contain("\"label\": \"Assembly-CSharp\""), "the three heaviest project assemblies: Assembly-CSharp by size, the others by fan-in");
            Assert.That(json, Does.Contain("\"label\": \"UniTask\""), "and the one external the project uses most, in the quarter of slots kept for them");
            Assert.That(json, Does.Not.Contain("\"label\": \"Unity.TextMeshPro\""));
            Assert.That(json, Does.Not.Contain("\"label\": \"Game.Editor\""), "folded into Other Editor");
        }

        [Test]
        public void Lists_the_findings_above_info_on_a_card()
        {
            string json = ArchifyArchitectureExporter.Export(ArchitectureFixtures.Report());

            Assert.That(json, Does.Contain("\"cards\": [\n    {\n      \"dot\": \"amber\",\n      \"title\": \"Findings above Info (2)\",\n      \"items\": [\n        \"Warning: Assembly-CSharp — 500 scripts compile outside any assembly definition (53.9 % of the project)\",\n        \"Advice: Game.Runtime — 312 scripts recompile on any change inside this assembly\"\n      ]\n    }\n  ]"));
        }

        [Test]
        public void Omits_the_card_when_nothing_is_above_info()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };
            report.Graph.AddNode(new GraphNode("a", "A", NodeKind.Runtime));

            Assert.That(ArchifyArchitectureExporter.Export(report), Does.Not.Contain("\"cards\""));
        }

        [Test]
        public void Squeezes_a_deep_chain_into_twelve_columns_without_sharing_cells()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };
            for (int i = 0; i < 20; i++)
            {
                report.Graph.AddNode(new GraphNode("n" + i, "N" + i, NodeKind.Runtime) { Weight = 20 - i });
            }

            for (int i = 0; i < 19; i++)
            {
                report.Graph.AddEdge(new GraphEdge("n" + i, "n" + (i + 1)));
            }

            CuratedGraph curated = GraphCuration.Curate(report.Graph, 20);
            string json = ArchifyArchitectureExporter.Write(report, curated, LayeredLayout.Compute(curated.Graph));

            MatchCollection cells = Regex.Matches(json, "\"row\": (\\d+),\\s*\"col\": (\\d+)");
            int[] columns = cells.Cast<Match>().Select(match => int.Parse(match.Groups[2].Value)).ToArray();
            Assert.That(columns.Max(), Is.EqualTo(ArchifyArchitectureExporter.MaxColumns - 1));
            Assert.That(cells.Cast<Match>().Select(match => int.Parse(match.Groups[1].Value)).Distinct().Count(), Is.EqualTo(20), "every node keeps its own row");
            Assert.That(json, Does.Contain("\"cols\": 12"));
            Assert.That(columns, Is.Ordered, "the chain keeps its direction");
        }

        [Test]
        public void Keeps_sources_within_the_schema_limits()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };
            var node = new GraphNode("a", "A", NodeKind.Runtime);
            node.Evidence.Add(new Evidence("Assets/A.cs", 12, 20, "definition"));
            node.Evidence.Add(new Evidence(new string('p', 241), 0, 0, "too long"));
            node.Evidence.Add(new Evidence("Assets/B.cs", 0, 0, new string('l', 60)));
            node.Evidence.Add(new Evidence("Assets/C.cs"));
            node.Evidence.Add(new Evidence("Assets/D.cs"));
            report.Graph.AddNode(node);

            string json = ArchifyArchitectureExporter.Export(report, 12, "Assembly map", new ArchifyRepository("https://github.com/a/b", new string('0', 40)));

            Assert.That(json, Does.Contain("\"path\": \"Assets/A.cs\",\n          \"line\": 12,\n          \"end_line\": 20,\n          \"label\": \"definition\""));
            Assert.That(json, Does.Not.Contain("ppppp"), "a path over 240 characters is skipped");
            Assert.That(json, Does.Contain("\"label\": \"" + new string('l', 47) + "…\""), "labels are cut to 48");
            Assert.That(json, Does.Contain("Assets/C.cs"));
            Assert.That(json, Does.Not.Contain("Assets/D.cs"), "at most three sources");
        }

        [Test]
        public void Every_identifier_satisfies_the_archify_rule()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };
            report.Graph.AddNode(new GraphNode("Game.Runtime", "Game.Runtime", NodeKind.Runtime) { Group = "Runtime 2024" });
            report.Graph.AddNode(new GraphNode("Game-Runtime", "Game-Runtime", NodeKind.Runtime) { Group = "Runtime 2024" });
            report.Graph.AddNode(new GraphNode("3rd.Party", "3rd.Party", NodeKind.External) { Group = "" });
            report.Graph.AddEdge(new GraphEdge("Game.Runtime", "3rd.Party"));

            string json = ArchifyArchitectureExporter.Export(report);

            foreach (Match match in Regex.Matches(json, "\"(?:id|from|to)\": \"([^\"]+)\""))
            {
                Assert.That(IdentifierSlug.IsValid(match.Groups[1].Value), Is.True, match.Groups[1].Value);
            }

            Assert.That(json, Does.Contain("\"id\": \"Game-Runtime\""));
            Assert.That(json, Does.Contain("\"id\": \"Game-Runtime-2\""));
            Assert.That(json, Does.Contain("\"id\": \"n3rd-Party\""));
            Assert.That(json, Does.Contain("\"id\": \"view-Runtime-2024\""));
        }

        [Test]
        public void Refuses_an_empty_graph()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };

            Assert.That(() => ArchifyArchitectureExporter.Export(report), Throws.InvalidOperationException);
        }

        [Test]
        public void Maps_every_kind_onto_one_component_type()
        {
            var types = new HashSet<string>();
            foreach (NodeKind kind in Enum.GetValues(typeof(NodeKind)))
            {
                types.Add(ArchifyArchitectureExporter.ComponentType(kind));
            }

            Assert.That(types, Is.EquivalentTo(new[] { "backend", "frontend", "database", "cloud", "messagebus", "security", "external" }));
        }
    }
}
