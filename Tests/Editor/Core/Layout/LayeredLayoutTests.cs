using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class LayeredLayoutTests
    {
        [Test]
        public void A_chain_reads_left_to_right_along_its_dependencies()
        {
            ReportGraph graph = Graph("a", "b", "c");
            graph.AddEdge(new GraphEdge("a", "b"));
            graph.AddEdge(new GraphEdge("b", "c"));

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.Columns, Is.EqualTo(3));
            Assert.That(layout.Position("a"), Is.EqualTo(new GridPosition(0, 0)));
            Assert.That(layout.Position("b"), Is.EqualTo(new GridPosition(1, 0)));
            Assert.That(layout.Position("c"), Is.EqualTo(new GridPosition(2, 0)));
            Assert.That(layout.BackEdgesIgnored, Is.EqualTo(0));
        }

        [Test]
        public void A_shared_dependency_sits_past_its_deepest_dependant()
        {
            ReportGraph graph = Graph("app", "feature", "core", "events");
            graph.AddEdge(new GraphEdge("app", "feature"));
            graph.AddEdge(new GraphEdge("app", "events"));
            graph.AddEdge(new GraphEdge("feature", "core"));
            graph.AddEdge(new GraphEdge("core", "events"));

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.Position("app").Column, Is.EqualTo(0));
            Assert.That(layout.Position("feature").Column, Is.EqualTo(1));
            Assert.That(layout.Position("core").Column, Is.EqualTo(2));
            Assert.That(layout.Position("events").Column, Is.EqualTo(3), "the longest path wins, not the shortest");
        }

        [Test]
        public void Unconnected_nodes_share_the_first_column()
        {
            ReportGraph graph = Graph("x", "y");

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.Columns, Is.EqualTo(1));
            Assert.That(layout.Rows(0), Is.EqualTo(2));
            Assert.That(layout.Position("x").Row, Is.EqualTo(0));
            Assert.That(layout.Position("y").Row, Is.EqualTo(1));
        }

        [Test]
        public void Rows_group_nodes_together_then_put_the_heaviest_first()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("light-ed", "Light", NodeKind.Editor) { Group = "Editor", Weight = 1 });
            graph.AddNode(new GraphNode("heavy-rt", "Heavy", NodeKind.Runtime) { Group = "Runtime", Weight = 9 });
            graph.AddNode(new GraphNode("mid-rt", "Mid", NodeKind.Runtime) { Group = "Runtime", Weight = 3 });
            graph.AddNode(new GraphNode("heavy-ed", "Heavy", NodeKind.Editor) { Group = "Editor", Weight = 9 });
            graph.AddNode(new GraphNode("also-rt", "Mid", NodeKind.Runtime) { Group = "Runtime", Weight = 3 });

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.Position("heavy-ed").Row, Is.EqualTo(0), "Editor sorts before Runtime, heaviest first");
            Assert.That(layout.Position("light-ed").Row, Is.EqualTo(1));
            Assert.That(layout.Position("heavy-rt").Row, Is.EqualTo(2));
            Assert.That(layout.Position("also-rt").Row, Is.EqualTo(3), "equal weight and label: id breaks the tie");
            Assert.That(layout.Position("mid-rt").Row, Is.EqualTo(4));
        }

        [Test]
        public void A_cycle_is_broken_deterministically_and_counted()
        {
            ReportGraph graph = Graph("a", "b", "c");
            graph.AddEdge(new GraphEdge("a", "b"));
            graph.AddEdge(new GraphEdge("b", "c"));
            graph.AddEdge(new GraphEdge("c", "a"));

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.BackEdgesIgnored, Is.EqualTo(1));
            Assert.That(layout.Columns, Is.EqualTo(3));
            Assert.That(layout.Position("a").Column, Is.EqualTo(0), "the search starts at the first node, so the edge back into it is the one dropped");
            Assert.That(layout.Position("b").Column, Is.EqualTo(1));
            Assert.That(layout.Position("c").Column, Is.EqualTo(2));
        }

        [Test]
        public void A_two_node_cycle_still_places_both_nodes()
        {
            ReportGraph graph = Graph("a", "b");
            graph.AddEdge(new GraphEdge("a", "b"));
            graph.AddEdge(new GraphEdge("b", "a"));

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.BackEdgesIgnored, Is.EqualTo(1));
            Assert.That(layout.Position("a").Column, Is.EqualTo(0));
            Assert.That(layout.Position("b").Column, Is.EqualTo(1));
        }

        [Test]
        public void Self_loops_duplicates_and_dangling_edges_are_ignored()
        {
            ReportGraph graph = Graph("a", "b");
            graph.AddEdge(new GraphEdge("a", "a"));
            graph.AddEdge(new GraphEdge("a", "b"));
            graph.AddEdge(new GraphEdge("a", "b"));
            graph.AddEdge(new GraphEdge("a", "ghost"));

            GraphLayout layout = LayeredLayout.Compute(graph);

            Assert.That(layout.BackEdgesIgnored, Is.EqualTo(0));
            Assert.That(layout.Position("b").Column, Is.EqualTo(1));
            GridPosition ignored;
            Assert.That(layout.TryGetPosition("ghost", out ignored), Is.False);
            Assert.That(layout.TryGetPosition(null, out ignored), Is.False);
        }

        [Test]
        public void An_empty_graph_has_no_columns()
        {
            GraphLayout layout = LayeredLayout.Compute(new ReportGraph());

            Assert.That(layout.Columns, Is.EqualTo(0));
            Assert.That(() => layout.Position("a"), Throws.TypeOf<KeyNotFoundException>());
        }

        [Test]
        public void Node_order_does_not_change_the_columns_or_rows_of_an_acyclic_graph()
        {
            var forward = new ReportGraph();
            forward.AddNode(new GraphNode("a", "A", NodeKind.Runtime) { Weight = 2 });
            forward.AddNode(new GraphNode("b", "B", NodeKind.Runtime) { Weight = 1 });
            forward.AddNode(new GraphNode("c", "C", NodeKind.Runtime) { Weight = 3 });
            forward.AddEdge(new GraphEdge("a", "c"));
            forward.AddEdge(new GraphEdge("b", "c"));
            var backward = new ReportGraph();
            backward.AddNode(new GraphNode("c", "C", NodeKind.Runtime) { Weight = 3 });
            backward.AddNode(new GraphNode("b", "B", NodeKind.Runtime) { Weight = 1 });
            backward.AddNode(new GraphNode("a", "A", NodeKind.Runtime) { Weight = 2 });
            backward.AddEdge(new GraphEdge("b", "c"));
            backward.AddEdge(new GraphEdge("a", "c"));

            GraphLayout first = LayeredLayout.Compute(forward);
            GraphLayout second = LayeredLayout.Compute(backward);

            foreach (string id in new[] { "a", "b", "c" })
            {
                Assert.That(second.Position(id), Is.EqualTo(first.Position(id)), id);
            }

            Assert.That(first.Position("a"), Is.EqualTo(new GridPosition(0, 0)), "heavier of the two roots first");
            Assert.That(first.Position("b"), Is.EqualTo(new GridPosition(0, 1)));
        }

        private static ReportGraph Graph(params string[] ids)
        {
            var graph = new ReportGraph();
            foreach (string id in ids)
            {
                graph.AddNode(new GraphNode(id, id.ToUpperInvariant(), NodeKind.Runtime));
            }

            return graph;
        }
    }
}
