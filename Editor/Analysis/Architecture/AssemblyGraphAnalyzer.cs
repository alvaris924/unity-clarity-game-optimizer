using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Settings;
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

        public Report Scan()
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;
            List<AssemblyDescription> descriptions = Describe(CompilationPipeline.GetAssemblies(), settings.ThirdPartyFolders);
            Report report = AssemblyGraphReport.Build(descriptions, settings.ArchitectureRules);
            ReportEnvironment.Stamp(report);
            return report;
        }

        /// <param name="thirdPartyFolders">Project-relative folders whose assemblies are plugins rather than the project's own code.</param>
        internal static List<AssemblyDescription> Describe(Assembly[] assemblies, IReadOnlyList<string> thirdPartyFolders)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var descriptions = new List<AssemblyDescription>(assemblies.Length);
            foreach (Assembly assembly in assemblies)
            {
                string definitionPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name) ?? "";
                AssemblyOrigin origin = OriginOf(definitionPath, thirdPartyFolders);
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

        /// <summary>Packages/ is a package, a third-party folder is a plugin inside the project, everything else is the project's own.</summary>
        internal static AssemblyOrigin OriginOf(string definitionPath, IReadOnlyList<string> thirdPartyFolders)
        {
            if (definitionPath.StartsWith(PackagesPrefix, StringComparison.Ordinal))
            {
                return AssemblyOrigin.Package;
            }

            return definitionPath.Length > 0 && FolderRules.IsUnderAny(definitionPath, thirdPartyFolders) ? AssemblyOrigin.Plugin : AssemblyOrigin.Project;
        }
    }
}
