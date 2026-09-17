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
        public void Architecture_is_the_only_area_that_scans_today()
        {
            AreaDescriptor architecture = AreaCatalog.Get(Area.Architecture);

            Assert.That(architecture.IsAvailable, Is.True);
            Assert.That(architecture.PlannedFor, Is.Empty);
            Assert.That(architecture.Scopes, Is.EqualTo(new[] { "Project" }));
            Assert.That(architecture.HeadlineMetrics, Is.EqualTo(new[] { "assemblies.count", "assemblies.project", "scripts.outside-definition-share", "assemblies.max-fan-in" }));
            Assert.That(architecture.Title, Is.EqualTo("Architecture"));
            foreach (AreaDescriptor other in AreaCatalog.All.Where(descriptor => descriptor.Area != Area.Architecture))
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
    }
}
