using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class DotAndMermaidExporterTests
    {
        [Test]
        public void Dot_clusters_groups_fills_by_kind_and_quotes_everything()
        {
            string dot = DotExporter.Write(Graph(), "Assembly map");

            Assert.That(dot, Does.StartWith("digraph \"Assembly map\" {\n  rankdir=LR;\n"));
            Assert.That(dot, Does.Contain("  subgraph \"cluster_Runtime\" {\n    label=\"Runtime\";\n"));
            Assert.That(dot, Does.Contain("    \"Game.Runtime\" [label=\"Game.Runtime\\n312 scripts\", fillcolor=\"#dbeafe\"];\n"));
            Assert.That(dot, Does.Contain("    \"Game.Tests\" [label=\"Game.Tests\", fillcolor=\"#dcfce7\"];\n"));
            Assert.That(dot, Does.Contain("  \"Say \\\"hi\\\"\" [label=\"Say \\\"hi\\\"\", fillcolor=\"#f1f5f9\"];\n"), "ungrouped nodes sit outside clusters, quotes escaped");
            Assert.That(dot, Does.Contain("  \"Game.Runtime\" -> \"Game.Tests\" [label=\"12 references\"];\n"));
            Assert.That(dot, Does.Contain("  \"Game.Tests\" -> \"Say \\\"hi\\\"\";\n"), "no label attribute for an unlabelled edge");
            Assert.That(dot, Does.EndWith("}\n"));
        }

        [Test]
        public void Mermaid_uses_safe_identifiers_subgraphs_and_entity_quotes()
        {
            string mermaid = MermaidExporter.Write(Graph());

            Assert.That(mermaid, Does.StartWith("flowchart LR\n"));
            Assert.That(mermaid, Does.Contain("  subgraph group_Runtime[\"Runtime\"]\n    Game_Runtime[\"Game.Runtime<br/>312 scripts\"]\n    Game_Tests[\"Game.Tests\"]\n  end\n"));
            Assert.That(mermaid, Does.Contain("  Say_hi[\"Say #quot;hi#quot;\"]\n"));
            Assert.That(mermaid, Does.Contain("  Game_Runtime -->|12 references| Game_Tests\n"));
            Assert.That(mermaid, Does.Contain("  Game_Tests --> Say_hi\n"));
        }

        [Test]
        public void Mermaid_guards_reserved_words_and_pipes()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("end", "The End", NodeKind.Runtime));
            graph.AddNode(new GraphNode("style", "Style", NodeKind.Runtime));
            graph.AddEdge(new GraphEdge("end", "style") { Label = "a|b" });

            string mermaid = MermaidExporter.Write(graph);

            Assert.That(mermaid, Does.Contain("  end_[\"The End\"]\n"));
            Assert.That(mermaid, Does.Contain("  style_[\"Style\"]\n"));
            Assert.That(mermaid, Does.Contain("  end_ -->|a/b| style_\n"));
        }

        [Test]
        public void Diagram_ids_are_unique_per_export_and_shared_by_edges()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("Game.Runtime", "A", NodeKind.Runtime));
            graph.AddNode(new GraphNode("Game-Runtime", "B", NodeKind.Runtime));
            graph.AddNode(new GraphNode("Game_Runtime", "C", NodeKind.Runtime));

            var ids = new DiagramIds(graph, MermaidExporter.MermaidId);

            Assert.That(ids.Of("Game.Runtime"), Is.EqualTo("Game_Runtime"));
            Assert.That(ids.Of("Game-Runtime"), Is.EqualTo("Game_Runtime-2"), "the suffix comes from the slug rule, after the transform");
            Assert.That(ids.Of("Game_Runtime"), Is.EqualTo("Game_Runtime-3"));
            Assert.That(ids.Reserve("view Game_Runtime"), Is.EqualTo("view_Game_Runtime"));
            Assert.That(ids.Has("missing"), Is.False);
        }

        private static ReportGraph Graph()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("Game.Runtime", "Game.Runtime", NodeKind.Runtime) { Group = "Runtime", Sublabel = "312 scripts" });
            graph.AddNode(new GraphNode("Game.Tests", "Game.Tests", NodeKind.Test) { Group = "Runtime" });
            graph.AddNode(new GraphNode("Say \"hi\"", "Say \"hi\"", NodeKind.External));
            graph.AddEdge(new GraphEdge("Game.Runtime", "Game.Tests") { Label = "12 references" });
            graph.AddEdge(new GraphEdge("Game.Tests", "Say \"hi\""));
            return graph;
        }
    }
}
