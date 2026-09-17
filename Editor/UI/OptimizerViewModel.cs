using System;
using System.Collections.Generic;
using System.Globalization;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.UI
{
    /// <summary>
    /// What the window shows, kept apart from how it is drawn so it can be tested without a panel: the
    /// selected area, the last report per area, the findings in display order, the headline metrics and
    /// the status line. Reports live only for the session; a domain reload starts empty, on purpose, since
    /// a recompile can invalidate what a scan read.
    /// </summary>
    internal sealed class OptimizerViewModel
    {
        private readonly Dictionary<Area, Report> _reports = new Dictionary<Area, Report>();
        private readonly Dictionary<Area, TimeSpan> _durations = new Dictionary<Area, TimeSpan>();
        private Area _selectedArea = Area.Architecture;
        private List<Finding> _findings = new List<Finding>();

        public event Action Changed;

        public Area SelectedArea
        {
            get { return _selectedArea; }
            set
            {
                if (_selectedArea == value)
                {
                    return;
                }

                _selectedArea = value;
                Refresh();
            }
        }

        public AreaDescriptor SelectedDescriptor
        {
            get { return AreaCatalog.Get(_selectedArea); }
        }

        /// <summary>The last report of the selected area, or null before its first scan.</summary>
        public Report Current
        {
            get
            {
                Report report;
                return _reports.TryGetValue(_selectedArea, out report) ? report : null;
            }
        }

        /// <summary>The findings of the current report, most severe first.</summary>
        public IReadOnlyList<Finding> Findings
        {
            get { return _findings; }
        }

        public void Store(Report report, TimeSpan duration)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            _reports[report.Area] = report;
            _durations[report.Area] = duration;
            Refresh();
        }

        public Report ReportFor(Area area)
        {
            Report report;
            return _reports.TryGetValue(area, out report) ? report : null;
        }

        /// <summary>The number of findings of an area's last report, or null before its first scan.</summary>
        public int? FindingCount(Area area)
        {
            Report report = ReportFor(area);
            return report == null ? (int?)null : report.Findings.Count;
        }

        /// <summary>The metrics the area's descriptor names, in that order, skipping any the report lacks; the first four when it names none.</summary>
        public IReadOnlyList<Metric> HeadlineMetrics
        {
            get
            {
                var headline = new List<Metric>();
                Report report = Current;
                if (report == null)
                {
                    return headline;
                }

                IReadOnlyList<string> keys = SelectedDescriptor.HeadlineMetrics;
                if (keys.Count == 0)
                {
                    for (int i = 0; i < report.Metrics.Count && i < 4; i++)
                    {
                        headline.Add(report.Metrics[i]);
                    }

                    return headline;
                }

                foreach (string key in keys)
                {
                    foreach (Metric metric in report.Metrics)
                    {
                        if (metric.Key == key)
                        {
                            headline.Add(metric);
                            break;
                        }
                    }
                }

                return headline;
            }
        }

        /// <summary>One line about the last scan of the selected area, or what to do first.</summary>
        public string ScanSummary
        {
            get
            {
                Report report = Current;
                if (report == null)
                {
                    AreaDescriptor descriptor = SelectedDescriptor;
                    return descriptor.IsAvailable
                        ? "Press Scan to read the project."
                        : descriptor.Title + " is not here yet: " + descriptor.PlannedFor.ToLowerInvariant() + ".";
                }

                TimeSpan duration;
                _durations.TryGetValue(report.Area, out duration);
                return "Scanned " + report.Scope + " in " + duration.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) + " s: "
                    + Plural(report.Graph.Nodes.Count, "node", "nodes") + ", "
                    + Plural(report.Findings.Count, "finding", "findings") + " ("
                    + report.CountFindings(Severity.Critical).ToString(CultureInfo.InvariantCulture) + " critical, "
                    + report.CountFindings(Severity.Warning).ToString(CultureInfo.InvariantCulture) + " warnings, "
                    + report.CountFindings(Severity.Advice).ToString(CultureInfo.InvariantCulture) + " advice, "
                    + report.CountFindings(Severity.Info).ToString(CultureInfo.InvariantCulture) + " info).";
            }
        }

        private void Refresh()
        {
            Report report = Current;
            _findings = report == null ? new List<Finding>() : FindingOrder.MostSevereFirst(report.Findings);
            Action changed = Changed;
            if (changed != null)
            {
                changed();
            }
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}
