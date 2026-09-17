using System;
using System.IO;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Tests.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Analysis
{
    public class ReportWriterTests
    {
        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ClarityGameOptimizer-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void DeleteFolder()
        {
            if (Directory.Exists(_folder))
            {
                Directory.Delete(_folder, true);
            }
        }

        [Test]
        public void Writes_json_and_markdown_without_a_byte_order_mark()
        {
            ReportFiles files = ReportWriter.Write(ReportFixtures.Architecture(), _folder);

            Assert.That(files.Json, Is.EqualTo(Path.Combine(_folder, "report.json")));
            Assert.That(files.Markdown, Is.EqualTo(Path.Combine(_folder, "report.md")));
            byte[] json = File.ReadAllBytes(files.Json);
            Assert.That(json[0], Is.EqualTo((byte)'{'), "no BOM");
            Assert.That(json[json.Length - 1], Is.EqualTo((byte)'\n'), "one trailing line break");
            Assert.That(File.ReadAllText(files.Markdown), Does.StartWith("# Architecture report\n"));
        }

        [Test]
        public void Refuses_an_invalid_report()
        {
            var report = new Report(Area.Assets, "Project");

            Assert.That(() => ReportWriter.Write(report, _folder), Throws.InvalidOperationException.With.Message.Contain("createdAt is not set"));
            Assert.That(Directory.Exists(_folder), Is.False, "nothing is written for an invalid report");
        }

        [Test]
        public void Area_folders_are_kebab_case()
        {
            Assert.That(Areas.FolderName(Area.Architecture), Is.EqualTo("architecture"));
            Assert.That(Areas.FolderName(Area.BuildSize), Is.EqualTo("build-size"));
            Assert.That(Areas.FolderName(Area.CodeQuality), Is.EqualTo("code-quality"));
            Assert.That(ReportFolders.ForArea(Area.BuildSize), Does.EndWith(Path.Combine("ClarityGameOptimizerReports", "build-size")));
        }
    }
}
