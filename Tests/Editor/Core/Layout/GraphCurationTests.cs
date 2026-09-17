using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class GraphCurationTests
    {
        [Test]
        public void Keeps_the_heaviest_nodes_and_folds_the_rest_into_one_node_per_group()
        {
            CuratedGraph curated = GraphCuration.Curate(Fixture(), 3);

            Assert.That(curated.Graph.Nodes.Select(node => node.Id), Is.EqualTo(new[]
            {
                "game", "events", "tmp", "other:Runtime", "other:Packages",
            }), "kept nodes in their original order, then one Other per group in group order");
            Assert.That(curated.CollapsedNodes, Is.EqualTo(4));

            GraphNode otherRuntime = Node(curated, "other:Runtime");
            Assert.That(otherRuntime.Label, Is.EqualTo("Other Runtime (2)"));
            Assert.That(otherRuntime.Kind, Is.EqualTo(NodeKind.Runtime));
            Assert.That(otherRuntime.Group, Is.EqualTo("Runtime"));
            Assert.That(otherRuntime.Weight, Is.EqualTo(3), "the sum of the folded weights");
            Assert.That(Node(curated, "other:Packages").Label, Is.EqualTo("Other Packages (2)"));
            Assert.That(Node(curated, "other:Packages").Kind, Is.EqualTo(NodeKind.External));
        }

        [Test]
        public void Kept_nodes_are_copies_with_their_evidence()
        {
            ReportGraph original = Fixture();

            CuratedGraph curated = GraphCuration.Curate(original, 3);

            GraphNode kept = Node(curated, "game");
            GraphNode source = original.Nodes.Single(node => node.Id == "game");
            Assert.That(kept, Is.Not.SameAs(source));
            Assert.That(kept.Sublabel, Is.EqualTo(source.Sublabel));
            Assert.That(kept.Evidence.Single().Path, Is.EqualTo("Assets/Game/Game.asmdef"));
        }

        [Test]
        public void Edges_follow_their_nodes_merge_with_a_count_and_never_loop_inside_a_folded_node()
        {
            CuratedGraph curated = GraphCuration.Curate(Fixture(), 3);

            string[] edges = curated.Graph.Edges.Select(edge => edge.From + " > " + edge.To + " [" + edge.Label + "] " + edge.Weight).ToArray();
            Assert.That(edges, Is.EqualTo(new[]
            {
                "game > events [references] 1",
                "game > tmp [references] 1",
                "game > other:Runtime [2 references] 2",
                "other:Runtime > events [2 references] 2",
                "other:Runtime > other:Packages [references] 1",
            }));
            Assert.That(curated.MergedEdges, Is.EqualTo(3), "two merged into counted edges, one vanished inside Other Runtime");
        }

        [Test]
        public void One_view_per_group_points_at_the_nodes_that_survived()
        {
            CuratedGraph curated = GraphCuration.Curate(Fixture(), 3);

            Assert.That(curated.Views.Select(view => view.Id), Is.EqualTo(new[] { "Runtime", "Packages" }));
            Assert.That(curated.Views[0].Label, Is.EqualTo("Runtime"));
            Assert.That(curated.Views[0].NodeIds, Is.EqualTo(new[] { "game", "events", "other:Runtime" }));
            Assert.That(curated.Views[1].NodeIds, Is.EqualTo(new[] { "tmp", "other:Packages" }));
        }

        [Test]
        public void Views_stop_at_five_groups()
        {
            var graph = new ReportGraph();
            for (int i = 0; i < 7; i++)
            {
                graph.AddNode(new GraphNode("n" + i, "N" + i, NodeKind.Runtime) { Group = "G" + i });
            }

            CuratedGraph curated = GraphCuration.Curate(graph, 12);

            Assert.That(curated.Views.Count, Is.EqualTo(GraphCuration.MaxViews));
            Assert.That(curated.Views.Select(view => view.Id), Is.EqualTo(new[] { "G0", "G1", "G2", "G3", "G4" }));
        }

        [Test]
        public void A_graph_within_the_limit_comes_back_whole()
        {
            ReportGraph original = Fixture();

            CuratedGraph curated = GraphCuration.Curate(original, 12);

            Assert.That(curated.Graph.Nodes.Select(node => node.Id), Is.EqualTo(original.Nodes.Select(node => node.Id)));
            Assert.That(curated.Graph.Edges.Count, Is.EqualTo(original.Edges.Count));
            Assert.That(curated.CollapsedNodes, Is.EqualTo(0));
            Assert.That(curated.MergedEdges, Is.EqualTo(0));
            Assert.That(curated.Graph, Is.Not.SameAs(original));
        }

        [Test]
        public void Ungrouped_nodes_fold_into_a_plain_other_node_and_view()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("big", "Big", NodeKind.Runtime) { Weight = 5 });
            graph.AddNode(new GraphNode("small", "Small", NodeKind.Editor) { Weight = 1 });
            graph.AddNode(new GraphNode("tiny", "Tiny", NodeKind.Editor) { Weight = 0 });

            CuratedGraph curated = GraphCuration.Curate(graph, 1);

            Assert.That(Node(curated, "other:").Label, Is.EqualTo("Other (2)"));
            Assert.That(Node(curated, "other:").Kind, Is.EqualTo(NodeKind.Editor), "the majority kind");
            Assert.That(curated.Views.Single().Id, Is.EqualTo("Ungrouped"));
        }

        [Test]
        public void Ties_in_weight_fall_back_to_label_then_id()
        {
            var graph = new ReportGraph();
            graph.AddNode(new GraphNode("z", "Same", NodeKind.Runtime) { Weight = 1 });
            graph.AddNode(new GraphNode("a", "Same", NodeKind.Runtime) { Weight = 1 });
            graph.AddNode(new GraphNode("m", "Alpha", NodeKind.Runtime) { Weight = 1 });

            CuratedGraph curated = GraphCuration.Curate(graph, 2);

            Assert.That(curated.Graph.Nodes.Select(node => node.Id), Is.EqualTo(new[] { "a", "m", "other:" }), "Alpha and the lower id survive; z folds");
        }

        [Test]
        public void The_curated_graph_survives_the_validator_inside_a_report()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };
            CuratedGraph curated = GraphCuration.Curate(Fixture(), 3);
            foreach (GraphNode node in curated.Graph.Nodes)
            {
                report.Graph.AddNode(node);
            }

            foreach (GraphEdge edge in curated.Graph.Edges)
            {
                report.Graph.AddEdge(edge);
            }

            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Rejects_a_null_graph_and_a_zero_limit()
        {
            Assert.That(() => GraphCuration.Curate(null), Throws.ArgumentNullException);
            Assert.That(() => GraphCuration.Curate(new ReportGraph(), 0), Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        private static GraphNode Node(CuratedGraph curated, string id)
        {
            GraphNode node;
            Assert.That(curated.Graph.TryGetNode(id, out node), Is.True, id);
            return node;
        }

        /// <summary>Seven nodes in two groups; with a limit of three, game, events and tmp survive.</summary>
        private static ReportGraph Fixture()
        {
            var graph = new ReportGraph();
            var game = new GraphNode("game", "Game", NodeKind.Runtime) { Group = "Runtime", Weight = 9, Sublabel = "312 scripts" };
            game.Evidence.Add(new Evidence("Assets/Game/Game.asmdef", 0, 0, "asmdef"));
            graph.AddNode(game);
            graph.AddNode(new GraphNode("events", "Events", NodeKind.Messaging) { Group = "Runtime", Weight = 8 });
            graph.AddNode(new GraphNode("feature-a", "Feature A", NodeKind.Runtime) { Group = "Runtime", Weight = 2 });
            graph.AddNode(new GraphNode("feature-b", "Feature B", NodeKind.Runtime) { Group = "Runtime", Weight = 1 });
            graph.AddNode(new GraphNode("tmp", "TextMeshPro", NodeKind.External) { Group = "Packages", Weight = 7 });
            graph.AddNode(new GraphNode("ugui", "UGUI", NodeKind.External) { Group = "Packages", Weight = 3 });
            graph.AddNode(new GraphNode("timeline", "Timeline", NodeKind.External) { Group = "Packages", Weight = 0 });
            graph.AddEdge(new GraphEdge("game", "events") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("game", "tmp") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("game", "feature-a") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("game", "feature-b") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("feature-a", "events") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("feature-b", "events") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("feature-a", "feature-b") { Label = "references", Weight = 1 });
            graph.AddEdge(new GraphEdge("feature-b", "ugui") { Label = "references", Weight = 1 });
            return graph;
        }
    }
}
