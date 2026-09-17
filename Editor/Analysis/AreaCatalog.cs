using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// The seven areas as the window sees them: what each one answers, which ones can scan today, which
    /// scopes they take and which metrics to put on the summary cards. The order is the order of the rail.
    /// </summary>
    internal static class AreaCatalog
    {
        private static readonly AreaDescriptor[] Areas =
        {
            new AreaDescriptor(Area.Architecture, "What is this project made of, and what depends on what?", "",
                new[] { AssemblyGraphReport.Scope }, () => new AssemblyGraphAnalyzer().Scan(),
                new[] { "assemblies.count", "assemblies.project", "scripts.outside-definition-share", "assemblies.max-fan-in" }),
            new AreaDescriptor(Area.Performance, "Where does the frame, and the first eight seconds, go?", "Planned for v1.0.0", new string[0], null, new string[0]),
            new AreaDescriptor(Area.Memory, "What does the device actually hold, and what is the biggest lever?", "Planned for v0.5.0", new string[0], null, new string[0]),
            new AreaDescriptor(Area.BuildSize, "Where does the download weight sit, and which rows are free wins?", "Planned for v0.1.0", new string[0], null, new string[0]),
            new AreaDescriptor(Area.Assets, "What is in the project that should not be, and who pulls each asset in?", "Planned for v0.5.0", new string[0], null, new string[0]),
            new AreaDescriptor(Area.Dependencies, "What did we pull in, from where, and is it pinned?", "",
                new[] { DependenciesReport.Scope }, () => new DependenciesAnalyzer().Scan(),
                new[] { "packages.count", "packages.direct", "packages.git-floating", "plugins.scripts-outside-definition" }),
            new AreaDescriptor(Area.CodeQuality, "What will slow the team down or the frame?", "Planned for v1.0.0", new string[0], null, new string[0]),
        };

        public static IReadOnlyList<AreaDescriptor> All
        {
            get { return Areas; }
        }

        public static AreaDescriptor Get(Area area)
        {
            foreach (AreaDescriptor descriptor in Areas)
            {
                if (descriptor.Area == area)
                {
                    return descriptor;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(area));
        }
    }

    /// <summary>One area's entry in the catalog.</summary>
    internal sealed class AreaDescriptor
    {
        private readonly Func<Report> _scan;

        public AreaDescriptor(Area area, string question, string plannedFor, string[] scopes, Func<Report> scan, string[] headlineMetrics)
        {
            Area = area;
            Question = question ?? "";
            PlannedFor = plannedFor ?? "";
            Scopes = scopes ?? Array.Empty<string>();
            HeadlineMetrics = headlineMetrics ?? Array.Empty<string>();
            _scan = scan;
        }

        public Area Area { get; }

        public string Title
        {
            get { return Areas.Describe(Area); }
        }

        /// <summary>The question the area answers, shown as its tooltip and its empty state.</summary>
        public string Question { get; }

        /// <summary>Empty when the area can scan; otherwise the release it is planned for.</summary>
        public string PlannedFor { get; }

        public bool IsAvailable
        {
            get { return _scan != null; }
        }

        /// <summary>The scopes the area offers, in words; the first is the default.</summary>
        public IReadOnlyList<string> Scopes { get; }

        /// <summary>Metric keys to show on the summary cards, in order; missing keys are skipped.</summary>
        public IReadOnlyList<string> HeadlineMetrics { get; }

        public Report Scan()
        {
            if (_scan == null)
            {
                throw new InvalidOperationException(Title + " cannot scan yet: " + PlannedFor + ".");
            }

            return _scan();
        }
    }
}
