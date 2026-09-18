using System.Linq;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Analysis
{
    public class AreaCatalogTests
    {
        [Test]
        public void Lists_the_seven_areas_in_rail_order()
        {
            Assert.That(AreaCatalog.All.Select(descriptor => descriptor.Area), Is.EqualTo(new[]
            {
                Area.Architecture, Area.Performance, Area.Memory, Area.BuildSize, Area.Assets, Area.Dependencies, Area.CodeQuality,
            }));
            Assert.That(AreaCatalog.All.All(descriptor => descriptor.Question.Length > 0), Is.True);
        }

        [Test]
        public void Architecture_build_size_and_dependencies_are_the_areas_that_scan_today()
        {
            AreaDescriptor architecture = AreaCatalog.Get(Area.Architecture);
            AreaDescriptor dependencies = AreaCatalog.Get(Area.Dependencies);
            AreaDescriptor buildSize = AreaCatalog.Get(Area.BuildSize);

            Assert.That(architecture.IsAvailable, Is.True);
            Assert.That(architecture.PlannedFor, Is.Empty);
            Assert.That(architecture.Scopes, Is.EqualTo(new[] { "Project" }));
            Assert.That(architecture.HeadlineMetrics, Is.EqualTo(new[] { "assemblies.count", "assemblies.project", "scripts.outside-definition-share", "assemblies.max-fan-in" }));
            Assert.That(architecture.Title, Is.EqualTo("Architecture"));
            Assert.That(dependencies.IsAvailable, Is.True);
            Assert.That(dependencies.HeadlineMetrics, Is.EqualTo(new[] { "packages.count", "packages.direct", "packages.git-floating", "plugins.scripts-outside-definition" }));
            Assert.That(buildSize.IsAvailable, Is.True);
            Assert.That(buildSize.Scopes, Is.EqualTo(new[] { "Last build", "Project" }));
            Assert.That(buildSize.HeadlineMetrics, Is.EqualTo(new[] { "build.shipped-bytes", "assets.packed-bytes", "textures.recoverable-bytes", "models.recoverable-bytes" }));
            foreach (AreaDescriptor other in AreaCatalog.All.Where(descriptor => descriptor.Area != Area.Architecture && descriptor.Area != Area.Dependencies && descriptor.Area != Area.BuildSize))
            {
                Assert.That(other.IsAvailable, Is.False, other.Title);
                Assert.That(other.PlannedFor, Does.StartWith("Planned for v"), other.Title);
                Assert.That(() => other.Scan(), Throws.InvalidOperationException, other.Title);
            }
        }

        [Test]
        public void Scanning_architecture_yields_a_valid_report()
        {
            Report report = AreaCatalog.Get(Area.Architecture).Scan();

            Assert.That(report.Area, Is.EqualTo(Area.Architecture));
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }

        [Test]
        public void Scanning_build_size_takes_the_scope_and_falls_back_to_the_first()
        {
            AreaDescriptor buildSize = AreaCatalog.Get(Area.BuildSize);

            Report project = buildSize.Scan("Project");
            Report fallback = buildSize.Scan("Everything");
            Report first = buildSize.Scan();

            Assert.That(project.Scope, Is.EqualTo("Project"));
            Assert.That(fallback.Scope, Is.EqualTo("Last build"));
            Assert.That(first.Scope, Is.EqualTo("Last build"));
            Assert.That(ReportValidator.Validate(project), Is.Empty);
            Assert.That(ReportValidator.Validate(first), Is.Empty);
        }

        [Test]
        public void Scanning_dependencies_yields_a_valid_report()
        {
            Report report = AreaCatalog.Get(Area.Dependencies).Scan();

            Assert.That(report.Area, Is.EqualTo(Area.Dependencies));
            Assert.That(ReportValidator.Validate(report), Is.Empty);
        }
    }
}
