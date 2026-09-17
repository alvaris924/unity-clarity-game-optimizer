using System;
using System.IO;
using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Tests.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Analysis
{
    public class DiagramToolingTests
    {
        private string _folder;

        [SetUp]
        public void CreateFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "ClarityGameOptimizer-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void DeleteFolder()
        {
            Directory.Delete(_folder, true);
        }

        [TestCase("plain", "plain")]
        [TestCase("", "\"\"")]
        [TestCase("has space", "\"has space\"")]
        [TestCase("say \"hi\"", "\"say \\\"hi\\\"\"")]
        [TestCase("C:\\Path\\", "C:\\Path\\")]
        [TestCase("C:\\Path with space\\", "\"C:\\Path with space\\\\\"")]
        [TestCase("back\\\"slash", "\"back\\\\\\\"slash\"")]
        public void Command_line_arguments_are_quoted_the_way_the_runtime_parses_them(string argument, string expected)
        {
            Assert.That(CommandLine.Quote(argument), Is.EqualTo(expected));
        }

        [Test]
        public void Command_line_arguments_are_joined_with_spaces()
        {
            Assert.That(CommandLine.Join(new[] { "validate", "architecture", "a b.json", "--json" }), Is.EqualTo("validate architecture \"a b.json\" --json"));
        }

        [Test]
        public void The_locator_accepts_the_root_the_skill_folder_or_the_bin_folder()
        {
            string root = Path.Combine(_folder, "archify");
            Directory.CreateDirectory(Path.Combine(root, "bin"));
            File.WriteAllText(Path.Combine(root, "bin", "archify.mjs"), "// stub");

            Assert.That(ArchifyLocator.At(root, "test").Root, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(ArchifyLocator.At(_folder, "test").Root, Is.EqualTo(Path.GetFullPath(root)), "the parent that holds an archify subfolder");
            Assert.That(ArchifyLocator.At(Path.Combine(root, "bin"), "test").Root, Is.EqualTo(Path.GetFullPath(root)));
            Assert.That(ArchifyLocator.At(root, "test").Script, Does.EndWith(Path.Combine("bin", "archify.mjs")));
            Assert.That(ArchifyLocator.At(Path.Combine(_folder, "nowhere"), "test"), Is.Null);
            Assert.That(ArchifyLocator.At("", "test"), Is.Null);
        }

        [Test]
        public void The_locator_honours_the_home_variable_before_the_skill_folders()
        {
            string previous = Environment.GetEnvironmentVariable(ArchifyLocator.HomeVariable);
            try
            {
                Environment.SetEnvironmentVariable(ArchifyLocator.HomeVariable, _folder);

                bool seen = false;
                foreach (var candidate in ArchifyLocator.Candidates())
                {
                    if (candidate.Key == ArchifyLocator.HomeVariable)
                    {
                        Assert.That(candidate.Value, Is.EqualTo(_folder));
                        seen = true;
                        break;
                    }

                    Assert.That(candidate.Key, Is.EqualTo("Preferences"), "only the user's own pick comes first");
                }

                Assert.That(seen, Is.True);
            }
            finally
            {
                Environment.SetEnvironmentVariable(ArchifyLocator.HomeVariable, previous);
            }
        }

        [Test]
        public void Runner_results_read_the_verdict_out_of_the_json()
        {
            Assert.That(ArchifyRunner.ParseOk("{\n  \"schemaVersion\": 1,\n  \"ok\": true,"), Is.True);
            Assert.That(ArchifyRunner.ParseOk("{\"ok\":false}"), Is.False);
            Assert.That(ArchifyRunner.ParseOk("not json"), Is.Null);
            Assert.That(ArchifyRunner.ParseStatus("\"composition\": {\n    \"status\": \"pass\""), Is.EqualTo("pass"));

            var passed = new ArchifyResult("node x", true, 0, "{\"ok\": true, \"composition\": {\"status\": \"pass\"}}", "", false);
            Assert.That(passed.Succeeded, Is.True);
            Assert.That(passed.Summary, Is.EqualTo("ok"));
            var failedComposition = new ArchifyResult("node x", true, 0, "{\"ok\": true, \"composition\": {\"status\": \"fail\"}}", "", false);
            Assert.That(failedComposition.Succeeded, Is.False);
            var crashed = new ArchifyResult("node x", true, 2, "", "boom\nmore", false);
            Assert.That(crashed.Succeeded, Is.False);
            Assert.That(crashed.Summary, Is.EqualTo("exit code 2: boom"));
            Assert.That(ArchifyResult.NotStarted("node x", "no node").Summary, Is.EqualTo("could not start: no node"));
            Assert.That(new ArchifyResult("node x", true, -1, "", "", true).Summary, Is.EqualTo("timed out"));
        }

        [Test]
        public void Node_version_is_null_or_a_version_string()
        {
            string version = ArchifyRunner.NodeVersion();

            Assert.That(version == null || version.StartsWith("v", StringComparison.Ordinal), Is.True, version);
        }

        [Test]
        public void The_diagram_writer_writes_the_three_files_beside_the_report()
        {
            DiagramFiles files = DiagramWriter.Write(ArchitectureFixtures.Report(), _folder);

            Assert.That(File.ReadAllText(files.Archify), Does.StartWith("{\n  \"schema_version\": 1,"));
            Assert.That(File.ReadAllText(files.Dot), Does.StartWith("digraph \"Assembly map\" {"));
            Assert.That(File.ReadAllText(files.Mermaid), Does.StartWith("flowchart LR\n"));
            Assert.That(files.Html, Is.EqualTo(Path.Combine(_folder, "assemblies.html")));
            Assert.That(File.Exists(files.Html), Is.False, "rendering is archify's job");
        }
    }
}
