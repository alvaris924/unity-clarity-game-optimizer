using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using UnityEditor.Compilation;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads the project's script assemblies from the compilation pipeline and builds the Architecture
    /// report. Everything that decides what the report says lives in <see cref="AssemblyGraphReport"/>;
    /// this class only describes what the Editor knows.
    /// </summary>
    internal sealed class AssemblyGraphAnalyzer
    {
        private const string PackagesPrefix = "Packages/";
        private const string PluginsPrefix = "Assets/Plugins/";

        public Report Scan()
        {
            List<AssemblyDescription> descriptions = Describe(CompilationPipeline.GetAssemblies());
            Report report = AssemblyGraphReport.Build(descriptions);
            ReportEnvironment.Stamp(report);
            return report;
        }

        internal static List<AssemblyDescription> Describe(Assembly[] assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var descriptions = new List<AssemblyDescription>(assemblies.Length);
            foreach (Assembly assembly in assemblies)
            {
                string definitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name) ?? "";
                AssemblyOrigin origin = OriginOf(definitionPath);
                var description = new AssemblyDescription(assembly.name)
                {
                    DefinitionPath = definitionPath,
                    SourceFileCount = assembly.sourceFiles.Length,
                    IsEditorOnly = (assembly.flags & AssemblyFlags.EditorAssembly) != 0,
                    IsTest = origin == AssemblyOrigin.Project && TestAssemblyRule.IsTest(AssemblyDefinitionFile.Read(definitionPath)),
                    Origin = origin,
                };
                foreach (Assembly reference in assembly.assemblyReferences)
                {
                    description.References.Add(reference.name);
                }

                descriptions.Add(description);
            }

            return descriptions;
        }

        /// <summary>Packages/ is a package, Assets/Plugins/ is third-party code inside the project, everything else is the project's own.</summary>
        internal static AssemblyOrigin OriginOf(string definitionPath)
        {
            if (definitionPath.StartsWith(PackagesPrefix, StringComparison.Ordinal))
            {
                return AssemblyOrigin.Package;
            }

            return definitionPath.StartsWith(PluginsPrefix, StringComparison.OrdinalIgnoreCase) ? AssemblyOrigin.Plugin : AssemblyOrigin.Project;
        }
    }
}
