using System;
using System.Linq;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Analysis
{
    /// <summary>Runs the real analyzer over the development project, where this package is itself a package.</summary>
    public class AssemblyGraphAnalyzerTests
    {
        [Test]
        public void Scans_the_development_project_into_a_valid_stamped_report()
        {
            Report report = new AssemblyGraphAnalyzer().Scan();

            Assert.That(ReportValidator.Validate(report), Is.Empty);
            Assert.That(report.Area, Is.EqualTo(Area.Architecture));
            Assert.That(report.UnityVersion, Is.Not.Empty);
            Assert.That(report.BuildTarget, Is.Not.Empty);
            Assert.That(report.PackageVersion, Is.Not.Empty, "the package manifest version");
            Assert.That(report.CreatedAt.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That((DateTime.UtcNow - report.CreatedAt).TotalMinutes, Is.LessThan(5));
        }

        [Test]
        public void Sees_this_package_as_external_and_its_tests_referencing_its_core()
        {
            Report report = new AssemblyGraphAnalyzer().Scan();

            GraphNode core;
            Assert.That(report.Graph.TryGetNode("ClarityGameOptimizer.Core", out core), Is.True);
            Assert.That(core.Kind, Is.EqualTo(NodeKind.External), "installed from the package folder, so it is a package here");
            Assert.That(core.Group, Is.EqualTo("Packages"));
            Assert.That(core.Evidence.Single().Path, Does.EndWith("ClarityGameOptimizer.Core.asmdef"));
            Assert.That(core.Weight, Is.GreaterThanOrEqualTo(5), "Analysis, Settings, Extensions, UI, Cli and Tests reference Core");

            GraphNode tests;
            Assert.That(report.Graph.TryGetNode("ClarityGameOptimizer.Tests", out tests), Is.True);
            Assert.That(report.Graph.Edges.Any(edge => edge.From == "ClarityGameOptimizer.Tests" && edge.To == "ClarityGameOptimizer.Core"), Is.True);
            Assert.That(report.Graph.Edges.Any(edge => edge.From == "ClarityGameOptimizer.Tests" && edge.To == "UnityEngine.TestRunner"), Is.True);
        }

        [Test]
        public void Counts_every_assembly_the_compilation_pipeline_knows()
        {
            Report report = new AssemblyGraphAnalyzer().Scan();

            Metric count = report.Metrics.Single(metric => metric.Key == "assemblies.count");
            Assert.That(count.Value.Value, Is.EqualTo(UnityEditor.Compilation.CompilationPipeline.GetAssemblies().Length));
            Assert.That(count.Value.Value, Is.GreaterThanOrEqualTo(7), "at least this package's seven assemblies");
        }
    }
}
