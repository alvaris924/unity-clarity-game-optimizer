using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ClarityGameOptimizer.Core;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClarityGameOptimizer.Tests.Core
{
    /// <summary>
    /// The diagram files under Tests/Fixtures/archify that the exporter tests compare against and the CI
    /// job validates with the real archify. Regenerate them after an intentional exporter change with
    /// <c>Unity -batchmode -projectPath DevProject~ -executeMethod ClarityGameOptimizer.Tests.Core.DiagramFixtures.Update -quit</c>.
    /// </summary>
    public static class DiagramFixtures
    {
        public const string FixtureFolder = "Tests/Fixtures/archify/architecture";

        private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);

        /// <summary>Every fixture, as (file name, diagram text).</summary>
        public static IEnumerable<KeyValuePair<string, string>> Expected()
        {
            Report report = ArchitectureFixtures.Report();
            yield return new KeyValuePair<string, string>("assemblies-whole.architecture.json", ArchifyArchitectureExporter.Export(report, 12) + "\n");
            yield return new KeyValuePair<string, string>("assemblies-folded.architecture.json", ArchifyArchitectureExporter.Export(report, 4) + "\n");
        }

        public static string PackageRoot()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(DiagramFixtures).Assembly);
            if (info == null)
            {
                throw new InvalidOperationException("The tests are not running from the package.");
            }

            return info.resolvedPath;
        }

        public static string PathOf(string fileName)
        {
            return Path.Combine(PackageRoot(), FixtureFolder.Replace('/', Path.DirectorySeparatorChar), fileName);
        }

        public static void Update()
        {
            foreach (KeyValuePair<string, string> fixture in Expected())
            {
                string path = PathOf(fixture.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, fixture.Value, Utf8WithoutBom);
                Console.WriteLine("wrote " + path);
            }
        }
    }
}
