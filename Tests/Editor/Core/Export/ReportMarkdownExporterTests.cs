using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class ReportMarkdownExporterTests
    {
        [Test]
        public void Starts_with_the_area_heading_and_the_environment_table()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.Architecture());

            Assert.That(markdown, Does.StartWith("# Architecture report\n\n| | |\n|---|---|\n| Scope | Project |\n| Unity | 6000.2.9f1 |\n| Build target | Android |\n| Generated | 2026-09-17T04:00:00Z by Clarity Game Optimizer 0.1.0 |\n\n"));
        }

        [Test]
        public void Uses_the_spaced_area_name()
        {
            var report = new Report(Area.BuildSize, "Project") { CreatedAt = ReportFixtures.Timestamp };

            Assert.That(ReportMarkdownExporter.Write(report), Does.StartWith("# Build Size report\n"));
        }

        [Test]
        public void Summarises_the_findings_by_severity()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.MixedBudgets());

            Assert.That(markdown, Does.Contain("**5 findings**: 1 critical, 2 warnings, 1 advice, 1 info.\n"));
        }

        [Test]
        public void Says_so_when_there_are_no_findings()
        {
            var report = new Report(Area.Assets, "Project") { CreatedAt = ReportFixtures.Timestamp };

            string markdown = ReportMarkdownExporter.Write(report);

            Assert.That(markdown, Does.Contain("\nNo findings.\n"));
            Assert.That(markdown, Does.Not.Contain("## Findings"));
            Assert.That(markdown, Does.Not.Contain("## Metrics"));
            Assert.That(markdown, Does.EndWith("No findings.\n"));
        }

        [Test]
        public void Writes_metrics_as_a_table_with_formatted_values()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.Architecture());

            Assert.That(markdown, Does.Contain("## Metrics\n\n| Metric | Value | Budget |\n|---|---:|---|\n| Assemblies | 62 | None |\n"));
        }

        [Test]
        public void Groups_findings_by_budget_and_never_mixes_download_with_runtime_ram()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.MixedBudgets());

            int ram = markdown.IndexOf("## Findings: Runtime RAM (3)", StringComparison.Ordinal);
            int download = markdown.IndexOf("## Findings: Download size (1)", StringComparison.Ordinal);
            int none = markdown.IndexOf("## Findings (1)", StringComparison.Ordinal);
            Assert.That(ram, Is.GreaterThan(0), "runtime RAM section");
            Assert.That(download, Is.GreaterThan(ram), "download section after runtime RAM");
            Assert.That(none, Is.GreaterThan(download), "unbudgeted section last");

            string ramSection = markdown.Substring(ram, download - ram);
            string downloadSection = markdown.Substring(download, none - download);
            Assert.That(ramSection, Does.Contain("Atlas_01.png").And.Contain("Atlas_02.png").And.Contain("Music.ogg"));
            Assert.That(ramSection, Does.Not.Contain("Loading.png"));
            Assert.That(downloadSection, Does.Contain("Loading.png"));
            Assert.That(downloadSection, Does.Not.Contain("Atlas_"));
        }

        [Test]
        public void Orders_a_table_by_severity_then_by_measured_size_then_by_subject()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.MixedBudgets());

            int atlas1 = markdown.IndexOf("`Assets/Atlas_01.png`", StringComparison.Ordinal);
            int atlas2 = markdown.IndexOf("`Assets/Atlas_02.png`", StringComparison.Ordinal);
            int music = markdown.IndexOf("`Assets/Audio/Music.ogg`", StringComparison.Ordinal);
            Assert.That(atlas1, Is.LessThan(atlas2), "equal severity and size: subject order");
            Assert.That(atlas2, Is.LessThan(music), "a Warning outranks larger Advice");
        }

        [Test]
        public void Writes_measured_and_projected_values_with_an_arrow()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.MixedBudgets());

            Assert.That(markdown, Does.Contain("| 11.9 MB → 716.8 KB |"));
            Assert.That(markdown, Does.Contain("| 9.3 MB → 2 MB |"));
            Assert.That(markdown, Does.Contain("| 44 MB |"));
        }

        [Test]
        public void Writes_a_finding_row_with_subject_and_evidence_in_code_font()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.Architecture());

            Assert.That(markdown, Does.Contain("| Severity | Subject | Finding | Measured | Verdict | Evidence |\n|---|---|---|---:|---|---|\n"));
            Assert.That(markdown, Does.Contain("| Info | `Game.Runtime` | Referenced by 9 assemblies | 9 |  | `Assets/Game.Runtime.asmdef` |\n"));
        }

        [Test]
        public void Escapes_pipes_and_flattens_line_breaks_in_cells()
        {
            var report = new Report(Area.Assets, "Folder | Assets") { CreatedAt = ReportFixtures.Timestamp };
            report.Findings.Add(new Finding("check", "a|b`c")
            {
                Title = "first line\nsecond line",
                Severity = Severity.Warning,
                Verdict = "keep | drop",
            });

            string markdown = ReportMarkdownExporter.Write(report);

            Assert.That(markdown, Does.Contain("| Scope | Folder \\| Assets |"));
            Assert.That(markdown, Does.Contain("| Warning | `a\\|b'c` | first line second line |  | keep \\| drop |  |"));
        }

        [Test]
        public void Writes_notes_as_a_bullet_list_at_the_end()
        {
            string markdown = ReportMarkdownExporter.Write(ReportFixtures.Architecture());

            Assert.That(markdown, Does.EndWith("## Notes\n\n- Counts come from CompilationPipeline.\n"));
        }

        [Test]
        public void Omits_the_build_target_row_when_there_is_none()
        {
            var report = new Report(Area.Architecture, "Project") { CreatedAt = ReportFixtures.Timestamp };

            Assert.That(ReportMarkdownExporter.Write(report), Does.Not.Contain("Build target"));
        }
    }
}
