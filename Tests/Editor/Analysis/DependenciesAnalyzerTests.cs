using System;
using System.IO;
using System.Linq;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using NUnit.Framework;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClarityGameOptimizer.Tests.Analysis
{
    /// <summary>Runs the real analyzer over the development project, where this package is a local dependency.</summary>
    public class DependenciesAnalyzerTests
    {
        [Test]
        public void Scans_the_development_project_into_a_valid_stamped_report()
        {
            Report report = new DependenciesAnalyzer().Scan();

            Assert.That(ReportValidator.Validate(report), Is.Empty);
            Assert.That(report.Area, Is.EqualTo(Area.Dependencies));
            Assert.That(report.PackageVersion, Is.Not.Empty);
            Assert.That(report.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
        }

        [Test]
        public void Sees_this_package_as_a_local_dependency_with_its_own_dependency()
        {
            Report report = new DependenciesAnalyzer().Scan();

            GraphNode self;
            Assert.That(report.Graph.TryGetNode("com.alvaris.clarity-game-optimizer", out self), Is.True);
            Assert.That(self.Group, Is.EqualTo("Local"), "installed as file:../..");
            Assert.That(self.Kind, Is.EqualTo(NodeKind.External));
            Assert.That(report.Graph.Edges.Any(edge => edge.From == "com.alvaris.clarity-game-optimizer" && edge.To == "com.unity.modules.uielements"), Is.True);
            Assert.That(report.Findings.Any(finding => finding.Id == "package.local:com.alvaris.clarity-game-optimizer"), Is.True);
            Assert.That(report.Metrics.Single(metric => metric.Key == "packages.count").Value.Value, Is.EqualTo(PackageInfo.GetAllRegisteredPackages().Length));
            Assert.That(report.Metrics.Single(metric => metric.Key == "packages.built-in").Value.Value, Is.GreaterThan(0));
        }

        [Test]
        public void Package_paths_resolve_by_prefix_and_by_resolved_folder()
        {
            PackageInfo[] registered = PackageInfo.GetAllRegisteredPackages();
            PackageInfo self = registered.Single(info => info.name == "com.alvaris.clarity-game-optimizer");
            string projectRoot = ReportFolders.ProjectRoot;

            Assert.That(DependenciesAnalyzer.PackageOf("Packages/com.unity.textmeshpro/Scripts/Runtime/x.asmdef", registered, projectRoot), Is.EqualTo("com.unity.textmeshpro"));
            Assert.That(DependenciesAnalyzer.PackageOf(Path.Combine(self.resolvedPath, "Editor", "Core", "x.dll"), registered, projectRoot), Is.EqualTo(self.name));
            Assert.That(DependenciesAnalyzer.PackageOf("Assets/Plugins/x.dll", registered, projectRoot), Is.Null);
            Assert.That(DependenciesAnalyzer.PackageOf("", registered, projectRoot), Is.Null);
            Assert.That(DependenciesAnalyzer.DisplayPath(Path.Combine(self.resolvedPath, "Editor", "x.dll"), registered, projectRoot), Is.EqualTo("Packages/com.alvaris.clarity-game-optimizer/Editor/x.dll"));
            Assert.That(DependenciesAnalyzer.DisplayPath("Assets/Plugins/x.dll", registered, projectRoot), Is.EqualTo("Assets/Plugins/x.dll"));
            Assert.That(DependenciesAnalyzer.DisplayPath(Path.Combine(projectRoot, "Assets", "y.dll"), registered, projectRoot), Is.EqualTo("Assets/y.dll"));
        }

        [Test]
        public void Plugins_folder_holds_one_plugin_per_subfolder_and_other_folders_are_one_plugin_each()
        {
            string root = Path.Combine(Path.GetTempPath(), "ClarityGameOptimizer-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "Assets", "Plugins", "Alpha"));
                Directory.CreateDirectory(Path.Combine(root, "Assets", "Plugins", "Beta"));
                Directory.CreateDirectory(Path.Combine(root, "Assets", "Feel"));
                File.WriteAllText(Path.Combine(root, "Assets", "Plugins", "Alpha", "Alpha.dll"), "x");
                File.WriteAllText(Path.Combine(root, "Assets", "Plugins", "Alpha", "Alpha.asmdef"), "{}");
                File.WriteAllText(Path.Combine(root, "Assets", "Feel", "Feel.dll"), "xx");

                var plugins = DependenciesAnalyzer.DescribePlugins(new[] { "Assets/Plugins", "Assets/Feel", "Assets/Missing" }, root);

                Assert.That(plugins.Select(plugin => plugin.Folder), Is.EqualTo(new[] { "Assets/Plugins/Alpha", "Assets/Plugins/Beta", "Assets/Feel" }));
                Assert.That(plugins[0].LibraryCount, Is.EqualTo(1));
                Assert.That(plugins[0].DefinitionCount, Is.EqualTo(1));
                Assert.That(plugins[0].Bytes, Is.EqualTo(3));
                Assert.That(plugins[2].Bytes, Is.EqualTo(2));
                Assert.That(DependenciesAnalyzer.DirectorySize(Path.Combine(root, "nowhere")), Is.EqualTo(-1));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void Sources_map_onto_the_report_kinds_and_defines_are_split()
        {
            Assert.That(DependenciesAnalyzer.SourceOf(PackageSource.Registry), Is.EqualTo(PackageSourceKind.Registry));
            Assert.That(DependenciesAnalyzer.SourceOf(PackageSource.LocalTarball), Is.EqualTo(PackageSourceKind.Tarball));
            Assert.That(DependenciesAnalyzer.SourceOf(PackageSource.Unknown), Is.EqualTo(PackageSourceKind.Unknown));
            Assert.That(DependenciesAnalyzer.Defines(), Is.All.Not.Empty);
        }
    }
}
