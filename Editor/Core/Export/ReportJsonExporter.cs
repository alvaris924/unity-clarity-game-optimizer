using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Writes a report as <c>report.json</c>, the file agents and CI jobs read and two runs are diffed by.
    /// The shape is public API: it starts with a format name and a version, keeps insertion order
    /// everywhere, and writes every field even when empty so readers never probe for keys.
    /// </summary>
    internal static class ReportJsonExporter
    {
        public const string Format = "clarity-game-optimizer/report";
        public const int FormatVersion = 1;

        public static string Write(Report report, bool indented = true)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            var json = new JsonWriter(indented);
            json.BeginObject();
            json.Name("format").Value(Format);
            json.Name("formatVersion").Value(FormatVersion);
            json.Name("area").Value(report.Area.ToString());
            json.Name("scope").Value(report.Scope);
            json.Name("unityVersion").Value(report.UnityVersion);
            json.Name("buildTarget").Value(report.BuildTarget);
            json.Name("packageVersion").Value(report.PackageVersion);
            json.Name("createdAt").Value(FormatTimestamp(report.CreatedAt));

            json.Name("metrics").BeginArray();
            foreach (Metric metric in report.Metrics)
            {
                json.BeginObject();
                json.Name("key").Value(metric.Key);
                json.Name("label").Value(metric.Label);
                json.Name("value").Value(metric.Value.Value);
                json.Name("unit").Value(metric.Value.Unit);
                json.Name("budget").Value(metric.Budget.ToString());
                json.EndObject();
            }

            json.EndArray();

            json.Name("findings").BeginArray();
            foreach (Finding finding in report.Findings)
            {
                WriteFinding(json, finding);
            }

            json.EndArray();

            json.Name("graph").BeginObject();
            json.Name("nodes").BeginArray();
            foreach (GraphNode node in report.Graph.Nodes)
            {
                json.BeginObject();
                json.Name("id").Value(node.Id);
                json.Name("label").Value(node.Label);
                json.Name("sublabel").Value(node.Sublabel);
                json.Name("kind").Value(node.Kind.ToString());
                json.Name("group").Value(node.Group);
                json.Name("weight").Value(node.Weight);
                WriteEvidence(json, node.Evidence);
                json.EndObject();
            }

            json.EndArray();
            json.Name("edges").BeginArray();
            foreach (GraphEdge edge in report.Graph.Edges)
            {
                json.BeginObject();
                json.Name("from").Value(edge.From);
                json.Name("to").Value(edge.To);
                json.Name("label").Value(edge.Label);
                json.Name("weight").Value(edge.Weight);
                json.EndObject();
            }

            json.EndArray();
            json.EndObject();

            json.Name("notes").BeginArray();
            foreach (string note in report.Notes)
            {
                json.Value(note);
            }

            json.EndArray();
            json.EndObject();
            return json.ToString();
        }

        /// <summary>ISO 8601 in UTC to the second, "2026-09-17T04:00:00Z"; a local time is converted first.</summary>
        public static string FormatTimestamp(DateTime time)
        {
            DateTime utc = time.Kind == DateTimeKind.Utc ? time : time.ToUniversalTime();
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        private static void WriteFinding(JsonWriter json, Finding finding)
        {
            json.BeginObject();
            json.Name("id").Value(finding.Id);
            json.Name("check").Value(finding.Check);
            json.Name("subject").Value(finding.Subject);
            json.Name("title").Value(finding.Title);
            json.Name("severity").Value(finding.Severity.ToString());
            json.Name("budget").Value(finding.Budget.ToString());
            WriteMeasurement(json, "measured", finding.Measured);
            WriteMeasurement(json, "projected", finding.Projected);
            json.Name("verdict").Value(finding.Verdict);
            json.Name("fixId").Value(finding.FixId);
            WriteEvidence(json, finding.Evidence);
            json.EndObject();
        }

        private static void WriteMeasurement(JsonWriter json, string name, Measurement? measurement)
        {
            json.Name(name);
            if (!measurement.HasValue)
            {
                json.Null();
                return;
            }

            json.BeginObject();
            json.Name("value").Value(measurement.Value.Value);
            json.Name("unit").Value(measurement.Value.Unit);
            json.EndObject();
        }

        private static void WriteEvidence(JsonWriter json, List<Evidence> evidence)
        {
            json.Name("evidence").BeginArray();
            foreach (Evidence item in evidence)
            {
                json.BeginObject();
                json.Name("path").Value(item.Path);
                json.Name("line").Value(item.Line);
                json.Name("endLine").Value(item.EndLine);
                json.Name("label").Value(item.Label);
                json.EndObject();
            }

            json.EndArray();
        }
    }
}
