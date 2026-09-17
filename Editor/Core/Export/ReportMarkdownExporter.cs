using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes a report as Markdown for a pull request comment or a docs page. Findings are grouped by budget,
    /// and the download and runtime RAM tables never share a row, because a reader who adds them up gets a
    /// number that means nothing. Within a table the most severe finding comes first, then the largest.
    /// </summary>
    internal static class ReportMarkdownExporter
    {
        public const string ProductName = "Clarity Game Optimizer";

        private const string Arrow = " → ";

        private static readonly Budget[] SectionOrder =
        {
            Budget.RuntimeRam, Budget.Download, Budget.FrameTime, Budget.IterationTime, Budget.None,
        };

        public static string Write(Report report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var text = new StringBuilder();
            WriteHeader(text, report);
            WriteSummary(text, report);
            WriteMetrics(text, report);
            WriteFindings(text, report);
            WriteNotes(text, report);
            return text.ToString().TrimEnd('\n') + "\n";
        }

        private static void WriteHeader(StringBuilder text, Report report)
        {
            text.Append("# ").Append(Areas.Describe(report.Area)).Append(" report\n\n");
            text.Append("| | |\n|---|---|\n");
            AppendRow(text, "Scope", report.Scope);
            AppendRow(text, "Unity", report.UnityVersion);
            if (report.BuildTarget.Length > 0)
            {
                AppendRow(text, "Build target", report.BuildTarget);
            }

            AppendRow(text, "Generated", ReportJsonExporter.FormatTimestamp(report.CreatedAt) + " by " + ProductName + " " + report.PackageVersion);
            text.Append('\n');
        }

        private static void WriteSummary(StringBuilder text, Report report)
        {
            int total = report.Findings.Count;
            if (total == 0)
            {
                text.Append("No findings.\n\n");
                return;
            }

            text.Append("**").Append(Plural(total, "finding", "findings")).Append("**: ");
            text.Append(Plural(report.CountFindings(Severity.Critical), "critical", "critical")).Append(", ");
            text.Append(Plural(report.CountFindings(Severity.Warning), "warning", "warnings")).Append(", ");
            text.Append(Plural(report.CountFindings(Severity.Advice), "advice", "advice")).Append(", ");
            text.Append(Plural(report.CountFindings(Severity.Info), "info", "info")).Append(".\n\n");
        }

        private static void WriteMetrics(StringBuilder text, Report report)
        {
            if (report.Metrics.Count == 0)
            {
                return;
            }

            text.Append("## Metrics\n\n| Metric | Value | Budget |\n|---|---:|---|\n");
            foreach (Metric metric in report.Metrics)
            {
                text.Append("| ").Append(Cell(metric.Label));
                text.Append(" | ").Append(Cell(metric.Value.ToString()));
                text.Append(" | ").Append(Budgets.Describe(metric.Budget)).Append(" |\n");
            }

            text.Append('\n');
        }

        private static void WriteFindings(StringBuilder text, Report report)
        {
            foreach (Budget budget in SectionOrder)
            {
                List<Finding> rows = report.Findings
                    .Where(finding => finding.Budget == budget)
                    .OrderByDescending(finding => finding.Severity)
                    .ThenByDescending(finding => finding.Measured.HasValue ? finding.Measured.Value.Value : double.NegativeInfinity)
                    .ThenBy(finding => finding.Subject, StringComparer.Ordinal)
                    .ToList();
                if (rows.Count == 0)
                {
                    continue;
                }

                text.Append("## Findings");
                if (budget != Budget.None)
                {
                    text.Append(": ").Append(Budgets.Describe(budget));
                }

                text.Append(" (").Append(rows.Count.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
                text.Append("| Severity | Subject | Finding | Measured | Verdict | Evidence |\n|---|---|---|---:|---|---|\n");
                foreach (Finding finding in rows)
                {
                    text.Append("| ").Append(finding.Severity);
                    text.Append(" | ").Append(Code(finding.Subject));
                    text.Append(" | ").Append(Cell(finding.Title));
                    text.Append(" | ").Append(Measured(finding));
                    text.Append(" | ").Append(Cell(finding.Verdict));
                    text.Append(" | ").Append(EvidenceCell(finding.Evidence)).Append(" |\n");
                }

                text.Append('\n');
            }
        }

        private static void WriteNotes(StringBuilder text, Report report)
        {
            if (report.Notes.Count == 0)
            {
                return;
            }

            text.Append("## Notes\n\n");
            foreach (string note in report.Notes)
            {
                text.Append("- ").Append(Line(note)).Append('\n');
            }

            text.Append('\n');
        }

        private static string Measured(Finding finding)
        {
            if (!finding.Measured.HasValue)
            {
                return "";
            }

            string measured = Cell(finding.Measured.Value.ToString());
            return finding.Projected.HasValue ? measured + Arrow + Cell(finding.Projected.Value.ToString()) : measured;
        }

        private static string EvidenceCell(List<Evidence> evidence)
        {
            if (evidence.Count == 0)
            {
                return "";
            }

            var cell = new StringBuilder();
            foreach (Evidence item in evidence)
            {
                if (cell.Length > 0)
                {
                    cell.Append(", ");
                }

                cell.Append(Code(item.Locator));
            }

            return cell.ToString();
        }

        private static void AppendRow(StringBuilder text, string name, string value)
        {
            text.Append("| ").Append(name).Append(" | ").Append(Cell(value)).Append(" |\n");
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }

        /// <summary>Text safe inside a table cell: one line, pipes escaped.</summary>
        private static string Cell(string value)
        {
            return Line(value ?? "").Replace("|", "\\|");
        }

        /// <summary>Text on one line.</summary>
        private static string Line(string value)
        {
            return (value ?? "").Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
        }

        /// <summary>A cell in code font; backticks inside would end it early, so they become apostrophes.</summary>
        private static string Code(string value)
        {
            return "`" + Cell(value).Replace('`', '\'') + "`";
        }
    }
}
