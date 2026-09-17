using System.Collections.Generic;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class DependenciesReportTests
    {
        [Test]
        public void The_fixture_yields_a_valid_dependencies_report()
        {
            Report report = Build();

            Assert.That(report.Area, Is.EqualTo(Area.Dependencies));
            report.CreatedAt = ReportFixtures.Timestamp;
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Packages_become_external_nodes_grouped_by_source_with_version_and_use()
        {
            Report report = Build();

            Assert.That(report.Graph.Nodes.Select(node => node.Id), Is.EqualTo(new[]
            {
                "com.cysharp.messagepipe", "com.cysharp.unitask", "com.unity.modules.ui", "com.unity.textmeshpro", "com.unity.timeline", "com.vendor.tool", "Assets/Feel", "Assets/Plugins/Odin",
            }), "packages by name, then plugins by folder");
            GraphNode tmp = Node(report, "com.unity.textmeshpro");
            Assert.That(tmp.Kind, Is.EqualTo(NodeKind.External));
            Assert.That(tmp.Group, Is.EqualTo("Registry"));
            Assert.That(tmp.Label, Is.EqualTo("TextMeshPro"));
            Assert.That(tmp.Sublabel, Is.EqualTo("v3.0.6 · used by 4"));
            Assert.That(tmp.Weight, Is.EqualTo(4 + 1 + 1), "four referrers, one dependant package, named in the manifest");
            Assert.That(tmp.Evidence.Select(evidence => evidence.Path), Is.EqualTo(new[] { "Packages/com.unity.textmeshpro/package.json", "Packages/manifest.json" }));
            Assert.That(Node(report, "com.unity.modules.ui").Group, Is.EqualTo("Built-in"));
            Assert.That(Node(report, "com.unity.modules.ui").Sublabel, Is.EqualTo("v1.0.0 · used by 0"));
            Assert.That(Node(report, "com.unity.timeline").Evidence.Count, Is.EqualTo(1), "a transitive package is not in the manifest");
            Assert.That(Node(report, "com.cysharp.unitask").Group, Is.EqualTo("Git"));
            Assert.That(Node(report, "com.cysharp.unitask").Sublabel, Is.EqualTo("a1b2c3d · used by 2"), "pinned: the short hash");
            Assert.That(Node(report, "com.cysharp.messagepipe").Sublabel, Is.EqualTo("1.8.2 @58516c3 · used by 0"), "a tag and what it resolved to");
            Assert.That(Node(report, "com.vendor.tool").Group, Is.EqualTo("Local"));
        }

        [Test]
        public void Plugins_become_external_nodes_in_their_own_group()
        {
            Report report = Build();

            GraphNode feel = Node(report, "Assets/Feel");
            Assert.That(feel.Label, Is.EqualTo("Feel"));
            Assert.That(feel.Group, Is.EqualTo("Plugins"));
            Assert.That(feel.Sublabel, Is.EqualTo("2 asmdefs · 1 DLL · 800 scripts, 120 outside"));
            Assert.That(feel.Weight, Is.EqualTo(2 + 1 + 16));
            Assert.That(feel.Evidence.Single().Path, Is.EqualTo("Assets/Feel"));
            Assert.That(Node(report, "Assets/Plugins/Odin").Sublabel, Is.EqualTo("0 asmdefs · 6 DLLs · 0 scripts"));
        }

        [Test]
        public void Edges_follow_package_dependencies_and_ignore_unknown_targets()
        {
            Report report = Build();

            Assert.That(report.Graph.Edges.Select(edge => edge.From + " > " + edge.To), Is.EqualTo(new[]
            {
                "com.unity.textmeshpro > com.unity.modules.ui",
                "com.unity.timeline > com.unity.modules.ui",
                "com.vendor.tool > com.unity.textmeshpro",
            }));
            Assert.That(report.Graph.Edges.All(edge => edge.Label == "depends on"), Is.True);
            Assert.That(report.Notes, Has.Some.EqualTo("1 dependency on packages outside the scan was ignored."));
        }

        [Test]
        public void Metrics_count_sources_pins_plugins_and_defines()
        {
            Report report = Build();

            Assert.That(Metric(report, "packages.count"), Is.EqualTo(6));
            Assert.That(Metric(report, "packages.direct"), Is.EqualTo(4));
            Assert.That(Metric(report, "packages.registry"), Is.EqualTo(2));
            Assert.That(Metric(report, "packages.git"), Is.EqualTo(2));
            Assert.That(Metric(report, "packages.git-floating"), Is.EqualTo(1));
            Assert.That(Metric(report, "packages.local"), Is.EqualTo(1));
            Assert.That(Metric(report, "packages.built-in"), Is.EqualTo(1));
            Assert.That(Metric(report, "packages.bytes"), Is.EqualTo(3000 + 2000 + 500 + 100), "unknown sizes are left out");
            Assert.That(Metric(report, "plugins.count"), Is.EqualTo(2));
            Assert.That(Metric(report, "plugins.libraries"), Is.EqualTo(7));
            Assert.That(Metric(report, "plugins.scripts"), Is.EqualTo(800));
            Assert.That(Metric(report, "plugins.scripts-outside-definition"), Is.EqualTo(120));
            Assert.That(Metric(report, "assemblies.name-collisions"), Is.EqualTo(1));
            Assert.That(Metric(report, "defines.count"), Is.EqualTo(2));
            Assert.That(report.Notes, Has.Some.EqualTo("Scripting defines for the active build target: ODIN_INSPECTOR, DOTWEEN."));
        }

        [Test]
        public void Findings_cover_floating_git_local_paths_unreferenced_packages_plugins_and_collisions()
        {
            Report report = Build();

            Assert.That(report.Findings.Select(finding => finding.Id), Is.EqualTo(new[]
            {
                "package.git-floating:com.cysharp.messagepipe",
                "package.unreferenced:com.cysharp.messagepipe",
                "package.local:com.vendor.tool",
                "plugin.outside-definition:Assets/Feel",
                "assembly.name-collision:Opsive.Managers",
            }));

            Finding floating = report.Findings[0];
            Assert.That(floating.Severity, Is.EqualTo(Severity.Advice), "a version tag moves rarely");
            Assert.That(floating.Title, Is.EqualTo("Follows the git tag 1.8.2; a tag can be moved, and this checkout resolved it to 58516c3"));
            Assert.That(floating.Verdict, Is.EqualTo("Pin the manifest to the commit: #58516c3abcdef0123456789abcdef0123456789a"));
            Assert.That(floating.Evidence.Single().Path, Is.EqualTo("Packages/manifest.json"));

            Finding unreferenced = report.Findings[1];
            Assert.That(unreferenced.Severity, Is.EqualTo(Severity.Info));
            Assert.That(unreferenced.Title, Does.StartWith("Named in the manifest, but no assembly definition of the project references it"));

            Finding local = report.Findings[2];
            Assert.That(local.Severity, Is.EqualTo(Severity.Info));
            Assert.That(local.Title, Does.StartWith("Resolved from a local path"));

            Finding plugin = report.Findings[3];
            Assert.That(plugin.Severity, Is.EqualTo(Severity.Advice));
            Assert.That(plugin.Title, Is.EqualTo("120 of 800 scripts compile into the predefined assemblies with the project's own code"));
            var lone = new PluginDescription("Assets/Plugins/Lone") { ScriptCount = 1, ScriptsOutsideDefinitions = 1 };
            Assert.That(DependenciesReport.Build(new List<PackageDescription>(), new[] { lone }, null, null).Findings.Single().Title, Does.StartWith("1 of 1 script compiles into"));
            Assert.That(plugin.Measured.Value.Value, Is.EqualTo(120));
            Assert.That(plugin.Verdict, Does.StartWith("Add an assembly definition at the plugin's root"));

            Finding collision = report.Findings[4];
            Assert.That(collision.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(collision.Title, Is.EqualTo("2 files claim the assembly name Opsive.Managers; Unity warns of hard-to-diagnose failures and crashes"));
            Assert.That(collision.Evidence.Select(evidence => evidence.Label), Is.EqualTo(new[] { "library", "asmdef" }));
        }

        [Test]
        public void A_git_package_on_a_branch_is_a_warning()
        {
            var package = new PackageDescription("com.example.tool") { Source = PackageSourceKind.Git, GitRevision = "main", GitHash = new string('b', 40), IsDirect = true, ProjectReferrers = 1 };
            var noRevision = new PackageDescription("com.example.other") { Source = PackageSourceKind.Git, GitRevision = "", GitHash = new string('c', 40), IsDirect = true, ProjectReferrers = 1 };

            Report report = DependenciesReport.Build(new[] { package, noRevision }, null, null, null);

            Assert.That(report.Findings.Count, Is.EqualTo(2));
            Assert.That(report.Findings.All(finding => finding.Severity == Severity.Warning), Is.True);
            Assert.That(report.Findings[1].Title, Does.StartWith("Follows the git branch main;"));
            Assert.That(report.Findings[0].Title, Does.StartWith("Follows the default git branch;"));
            Assert.That(package.IsGitOnBranch, Is.True);
            Assert.That(package.IsGitOnVersionTag, Is.False);
        }

        [Test]
        public void Nothing_to_report_yields_zero_metrics_and_no_findings()
        {
            Report report = DependenciesReport.Build(new List<PackageDescription>(), null, null, null);

            Assert.That(report.Graph.IsEmpty);
            Assert.That(report.Findings, Is.Empty);
            Assert.That(Metric(report, "packages.count"), Is.EqualTo(0));
            report.CreatedAt = ReportFixtures.Timestamp;
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Descriptions_reject_blank_names_and_short_collisions()
        {
            Assert.That(() => new PackageDescription(" "), Throws.ArgumentException);
            Assert.That(() => new PluginDescription(""), Throws.ArgumentException);
            Assert.That(() => new AssemblyNameCollision("X", new[] { "one" }), Throws.ArgumentException);
            Assert.That(new PluginDescription("Assets\\Plugins\\Odin\\").Folder, Is.EqualTo("Assets/Plugins/Odin"));
            Assert.That(new PluginDescription("Assets/Plugins/Odin").Name, Is.EqualTo("Odin"));
            Assert.That(new PackageDescription("com.x").DisplayName, Is.EqualTo("com.x"), "falls back to the name");
        }

        private static Report Build()
        {
            return DependenciesReport.Build(Packages(), Plugins(), Collisions(), new[] { "ODIN_INSPECTOR", "DOTWEEN" });
        }

        private static double Metric(Report report, string key)
        {
            return report.Metrics.Single(metric => metric.Key == key).Value.Value;
        }

        private static GraphNode Node(Report report, string id)
        {
            GraphNode node;
            Assert.That(report.Graph.TryGetNode(id, out node), Is.True, id);
            return node;
        }

        private static List<PackageDescription> Packages()
        {
            var tmp = new PackageDescription("com.unity.textmeshpro") { DisplayName = "TextMeshPro", Version = "3.0.6", Source = PackageSourceKind.Registry, IsDirect = true, Bytes = 3000, ProjectReferrers = 4 };
            tmp.Dependencies.Add("com.unity.modules.ui");
            var timeline = new PackageDescription("com.unity.timeline") { DisplayName = "Timeline", Version = "1.8.7", Source = PackageSourceKind.Registry, IsDirect = false, Bytes = 2000 };
            timeline.Dependencies.Add("com.unity.modules.ui");
            timeline.Dependencies.Add("com.unity.modules.director");
            var ui = new PackageDescription("com.unity.modules.ui") { DisplayName = "UI", Version = "1.0.0", Source = PackageSourceKind.BuiltIn, IsDirect = true, Bytes = -1 };
            var unitask = new PackageDescription("com.cysharp.unitask") { DisplayName = "UniTask", Version = "2.5.0", Source = PackageSourceKind.Git, IsDirect = true, GitRevision = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", GitHash = "a1b2c3d4e5f60718293a4b5c6d7e8f9012345678", Bytes = 500, ProjectReferrers = 2 };
            var messagePipe = new PackageDescription("com.cysharp.messagepipe") { DisplayName = "MessagePipe", Version = "1.8.2", Source = PackageSourceKind.Git, IsDirect = true, GitRevision = "1.8.2", GitHash = "58516c3abcdef0123456789abcdef0123456789a", Bytes = 100 };
            var local = new PackageDescription("com.vendor.tool") { DisplayName = "Vendor Tool", Version = "0.1.0", Source = PackageSourceKind.Local, IsDirect = false, Bytes = -1, ProjectReferrers = 1 };
            local.Dependencies.Add("com.unity.textmeshpro");
            return new List<PackageDescription> { tmp, timeline, ui, unitask, messagePipe, local };
        }

        private static List<PluginDescription> Plugins()
        {
            return new List<PluginDescription>
            {
                new PluginDescription("Assets/Plugins/Odin") { LibraryCount = 6, Bytes = 9000 },
                new PluginDescription("Assets/Feel") { DefinitionCount = 2, LibraryCount = 1, ScriptCount = 800, ScriptsOutsideDefinitions = 120, Bytes = 50000 },
            };
        }

        private static List<AssemblyNameCollision> Collisions()
        {
            return new List<AssemblyNameCollision>
            {
                new AssemblyNameCollision("Opsive.Managers", new[] { "Packages/com.opsive.behaviordesigner/Editor/Managers/Opsive.Managers.dll", "Packages/com.opsive.behaviordesigner/Editor/Managers/Opsive.Managers.asmdef" }),
            };
        }
    }
}
