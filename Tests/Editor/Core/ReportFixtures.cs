using System;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Tests.Core
{
    /// <summary>Small, fully populated reports the exporter and validator tests share.</summary>
    internal static class ReportFixtures
    {
        public static readonly DateTime Timestamp = new DateTime(2026, 9, 17, 4, 0, 0, DateTimeKind.Utc);

        /// <summary>One metric, one finding with evidence, two nodes and one edge, one note.</summary>
        public static Report Architecture()
        {
            var report = new Report(Area.Architecture, "Project")
            {
                UnityVersion = "6000.2.9f1",
                BuildTarget = "Android",
                PackageVersion = "0.1.0",
                CreatedAt = Timestamp,
            };
            report.Metrics.Add(new Metric("assemblies", Measurement.Count(62), Budget.None, "Assemblies"));

            var finding = new Finding("assembly.fan-in", "Game.Runtime")
            {
                Title = "Referenced by 9 assemblies",
                Severity = Severity.Info,
                Measured = Measurement.Count(9),
            };
            finding.Evidence.Add(new Evidence("Assets/Game.Runtime.asmdef", 0, 0, "asmdef"));
            report.Findings.Add(finding);

            report.Graph.AddNode(new GraphNode("game", "Game.Runtime", NodeKind.Runtime) { Sublabel = "312 scripts", Group = "Runtime", Weight = 9 });
            report.Graph.AddNode(new GraphNode("cbs", "PlayFab CBS", NodeKind.Service) { Group = "Runtime" });
            report.Graph.AddEdge(new GraphEdge("game", "cbs") { Label = "references", Weight = 1 });
            report.Notes.Add("Counts come from CompilationPipeline.");
            return report;
        }

        /// <summary>Findings in two size budgets plus one without, to prove the tables never mix.</summary>
        public static Report MixedBudgets()
        {
            var report = new Report(Area.Memory, "Last build (Android)")
            {
                UnityVersion = "6000.2.9f1",
                BuildTarget = "Android",
                PackageVersion = "0.1.0",
                CreatedAt = Timestamp,
            };
            report.Findings.Add(new Finding("texture.max-size", "Assets/Atlas_01.png")
            {
                Title = "4096 atlas without a platform override",
                Severity = Severity.Warning,
                Budget = Budget.RuntimeRam,
                Measured = Measurement.Bytes(12480102),
                Projected = Measurement.Bytes(734003),
                Verdict = "Max Size 1024",
                FixId = "texture.max-size",
            });
            report.Findings.Add(new Finding("texture.stored-uncompressed", "Assets/UI/Loading.png")
            {
                Title = "Stored as RGB24: 1593x2048 is not divisible by 4",
                Severity = Severity.Critical,
                Budget = Budget.Download,
                Measured = Measurement.Bytes(9781248),
                Projected = Measurement.Bytes(2065920),
                Verdict = "Lift the Max Size clamp, then force ETC2",
            });
            report.Findings.Add(new Finding("texture.max-size", "Assets/Atlas_02.png")
            {
                Title = "4096 atlas without a platform override",
                Severity = Severity.Warning,
                Budget = Budget.RuntimeRam,
                Measured = Measurement.Bytes(12480102),
                Verdict = "Max Size 1024",
            });
            report.Findings.Add(new Finding("audio.preload", "Assets/Audio/Music.ogg")
            {
                Title = "Preloaded music clip",
                Severity = Severity.Advice,
                Budget = Budget.RuntimeRam,
                Measured = Measurement.Bytes(46137344),
                Verdict = "Streaming",
            });
            report.Findings.Add(new Finding("scan.scope", "Last build")
            {
                Title = "1,026 textures in the build",
                Severity = Severity.Info,
                Measured = Measurement.Count(1026),
            });
            return report;
        }
    }
}
