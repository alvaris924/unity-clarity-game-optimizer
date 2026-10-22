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
        public void Every_area_but_assets_scans_today()
        {
            AreaDescriptor architecture = AreaCatalog.Get(Area.Architecture);
            AreaDescriptor dependencies = AreaCatalog.Get(Area.Dependencies);
            AreaDescriptor buildSize = AreaCatalog.Get(Area.BuildSize);
            AreaDescriptor memory = AreaCatalog.Get(Area.Memory);
            AreaDescriptor performance = AreaCatalog.Get(Area.Performance);
            AreaDescriptor codeQuality = AreaCatalog.Get(Area.CodeQuality);

            Assert.That(memory.IsAvailable, Is.True);
            Assert.That(memory.Scopes, Is.EqualTo(new[] { "Build scenes and Resources" }));
            Assert.That(memory.HeadlineMetrics, Is.EqualTo(new[] { "memory.total-bytes", "textures.runtime-bytes", "meshes.runtime-bytes", "audio.runtime-bytes" }));
            Assert.That(performance.IsAvailable, Is.True);
            Assert.That(performance.Scopes, Is.EqualTo(new[] { "Project settings and open scenes" }));
            Assert.That(performance.HeadlineMetrics, Is.EqualTo(new[] { "settings.flagged", "settings.checked", "cameras.count", "lights.realtime-directional-max" }));
            Assert.That(codeQuality.IsAvailable, Is.True);
            Assert.That(codeQuality.Scopes, Is.EqualTo(new[] { "Project scripts" }));
            Assert.That(codeQuality.HeadlineMetrics, Is.EqualTo(new[] { "hits.per-frame", "methods.per-frame", "messages.empty", "queries.no-mask" }));

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
            foreach (AreaDescriptor other in AreaCatalog.All.Where(descriptor => descriptor.Area == Area.Assets))
            {
                Assert.That(other.IsAvailable, Is.False, other.Title);
                Assert.That(other.PlannedFor, Does.StartWith("Planned for v"), other.Title);
                Assert.That(() => other.Scan(), Throws.InvalidOperationException, other.Title);
            }
        }

        [TestCase("Memory")]
        [TestCase("Performance")]
        [TestCase("CodeQuality")]
        public void Scanning_the_settings_memory_and_code_areas_yields_a_valid_report(string name)
        {
            // Area is internal, so the cases name it.
            var area = (Area)System.Enum.Parse(typeof(Area), name);
            Report report = AreaCatalog.Get(area).Scan();
            Assert.That(report.Area, Is.EqualTo(area));
            Assert.That(ReportValidator.Validate(report), Is.Empty);
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
