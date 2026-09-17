using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>Writes a report to a folder as <c>report.json</c> and <c>report.md</c>, after checking that it is valid.</summary>
    internal static class ReportWriter
    {
        public const string JsonFileName = "report.json";
        public const string MarkdownFileName = "report.md";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        public static ReportFiles Write(Report report, string folder)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A folder is needed.", nameof(folder));
            }

            List<string> problems = ReportValidator.Validate(report);
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("The report is not valid: " + string.Join("; ", problems));
            }

            Directory.CreateDirectory(folder);
            string jsonPath = Path.Combine(folder, JsonFileName);
            string markdownPath = Path.Combine(folder, MarkdownFileName);
            File.WriteAllText(jsonPath, ReportJsonExporter.Write(report) + "\n", Utf8WithoutBom);
            File.WriteAllText(markdownPath, ReportMarkdownExporter.Write(report), Utf8WithoutBom);
            return new ReportFiles(jsonPath, markdownPath);
        }
    }

    /// <summary>The files one write produced.</summary>
    internal readonly struct ReportFiles
    {
        public ReportFiles(string json, string markdown)
        {
            Json = json;
            Markdown = markdown;
        }

        public string Json { get; }

        public string Markdown { get; }
    }
}
