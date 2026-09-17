using System;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class ReportJsonExporterTests
    {
        [Test]
        public void Writes_the_whole_report_in_a_fixed_field_order()
        {
            string json = ReportJsonExporter.Write(ReportFixtures.Architecture(), indented: false);

            const string expected =
                "{\"format\":\"clarity-game-optimizer/report\",\"formatVersion\":1," +
                "\"area\":\"Architecture\",\"scope\":\"Project\",\"unityVersion\":\"6000.2.9f1\"," +
                "\"buildTarget\":\"Android\",\"packageVersion\":\"0.1.0\",\"createdAt\":\"2026-09-17T04:00:00Z\"," +
                "\"metrics\":[{\"key\":\"assemblies\",\"label\":\"Assemblies\",\"value\":62,\"unit\":\"count\",\"budget\":\"None\"}]," +
                "\"findings\":[{\"id\":\"assembly.fan-in:Game.Runtime\",\"check\":\"assembly.fan-in\",\"subject\":\"Game.Runtime\"," +
                "\"title\":\"Referenced by 9 assemblies\",\"severity\":\"Info\",\"budget\":\"None\"," +
                "\"measured\":{\"value\":9,\"unit\":\"count\"},\"projected\":null,\"verdict\":\"\",\"fixId\":null," +
                "\"evidence\":[{\"path\":\"Assets/Game.Runtime.asmdef\",\"line\":0,\"endLine\":0,\"label\":\"asmdef\"}]}]," +
                "\"graph\":{\"nodes\":[" +
                "{\"id\":\"game\",\"label\":\"Game.Runtime\",\"sublabel\":\"312 scripts\",\"kind\":\"Runtime\",\"group\":\"Runtime\",\"weight\":9,\"evidence\":[]}," +
                "{\"id\":\"cbs\",\"label\":\"PlayFab CBS\",\"sublabel\":\"\",\"kind\":\"Service\",\"group\":\"Runtime\",\"weight\":0,\"evidence\":[]}]," +
                "\"edges\":[{\"from\":\"game\",\"to\":\"cbs\",\"label\":\"references\",\"weight\":1}]}," +
                "\"notes\":[\"Counts come from CompilationPipeline.\"]}";
            Assert.That(json, Is.EqualTo(expected));
        }

        [Test]
        public void Indents_by_default_and_ends_without_a_trailing_line_break()
        {
            string json = ReportJsonExporter.Write(ReportFixtures.Architecture());

            Assert.That(json, Does.StartWith("{\n  \"format\": \"clarity-game-optimizer/report\",\n  \"formatVersion\": 1,\n"));
            Assert.That(json, Does.EndWith("\n}"));
            Assert.That(json, Does.Not.Contain("\r"));
        }

        [Test]
        public void Writes_an_empty_report_with_every_section_present()
        {
            var report = new Report(Area.Assets, "Project") { CreatedAt = ReportFixtures.Timestamp };

            string json = ReportJsonExporter.Write(report, indented: false);

            Assert.That(json, Does.Contain("\"metrics\":[]"));
            Assert.That(json, Does.Contain("\"findings\":[]"));
            Assert.That(json, Does.Contain("\"graph\":{\"nodes\":[],\"edges\":[]}"));
            Assert.That(json, Does.Contain("\"notes\":[]"));
            Assert.That(json, Does.Contain("\"buildTarget\":\"\""));
        }

        [Test]
        public void Converts_a_local_timestamp_to_utc()
        {
            var local = new DateTime(2026, 9, 17, 12, 0, 0, DateTimeKind.Local);
            string expected = local.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);

            Assert.That(ReportJsonExporter.FormatTimestamp(local), Is.EqualTo(expected));
        }

        [Test]
        public void Keeps_a_utc_timestamp_as_it_is()
        {
            Assert.That(ReportJsonExporter.FormatTimestamp(ReportFixtures.Timestamp), Is.EqualTo("2026-09-17T04:00:00Z"));
        }

        [Test]
        public void Rejects_a_null_report()
        {
            Assert.That(() => ReportJsonExporter.Write(null), Throws.ArgumentNullException);
        }
    }
}
