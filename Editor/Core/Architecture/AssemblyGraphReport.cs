using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns the assemblies of a project into the Architecture report: one node per assembly typed by role
    /// and grouped by boundary, one edge per reference, and as the weight how much the project leans on a
    /// node (fan-in from the project's own assemblies, since packages referencing each other would drown
    /// everything else out) plus how much code it holds (a point per fifty scripts), so that curation keeps
    /// both the hubs and the heavyweights; and findings for what slows
    /// a team down: scripts outside any assembly definition, hubs everything depends on, and assemblies
    /// large enough that any change inside recompiles a lot. Code under Packages/ and Assets/Plugins/ is
    /// drawn but never judged: it is not the project's to split. Pure: the Editor side only supplies the
    /// descriptions, and the output is the same for the same input on every machine.
    /// </summary>
    internal static class AssemblyGraphReport
    {
        public const string Scope = "Project";

        public const string OutsideDefinitionCheck = "assembly.outside-definition";
        public const string HubCheck = "assembly.hub";
        public const string LargeCheck = "assembly.large";

        /// <summary>Fan-in from the project's own assemblies at which an assembly counts as a hub worth knowing about.</summary>
        public const int HubFanIn = 5;

        /// <summary>Source files from which a project assembly counts as large.</summary>
        public const int LargeSourceFiles = 250;

        /// <summary>Every this many scripts add one point to a node's weight, next to one point per project assembly that references it.</summary>
        public const int ScriptsPerWeightPoint = 50;

        private const string ReferenceLabel = "references";

        public static Report Build(IReadOnlyList<AssemblyDescription> assemblies)
        {
            if (assemblies == null)
            {
                throw new ArgumentNullException(nameof(assemblies));
            }

            var sorted = new List<AssemblyDescription>(assemblies);
            sorted.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

            var report = new Report(Area.Architecture, Scope);
            var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            var fanIn = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (AssemblyDescription assembly in sorted)
            {
                if (nodes.ContainsKey(assembly.Name))
                {
                    continue;
                }

                NodeKind kind = KindOf(assembly);
                var node = new GraphNode(assembly.Name, assembly.Name, kind) { Group = GroupOf(assembly, kind) };
                if (assembly.DefinitionPath.Length > 0)
                {
                    node.Evidence.Add(new Evidence(assembly.DefinitionPath, 0, 0, "asmdef"));
                }

                nodes.Add(assembly.Name, report.Graph.AddNode(node));
                fanIn.Add(assembly.Name, 0);
            }

            int ignoredReferences = 0;
            foreach (AssemblyDescription assembly in sorted)
            {
                var targets = new List<string>(assembly.References);
                targets.Sort(string.CompareOrdinal);
                string previous = null;
                foreach (string target in targets)
                {
                    if (target == previous)
                    {
                        continue;
                    }

                    previous = target;
                    if (target == assembly.Name)
                    {
                        continue;
                    }

                    if (!nodes.ContainsKey(target))
                    {
                        ignoredReferences++;
                        continue;
                    }

                    report.Graph.AddEdge(new GraphEdge(assembly.Name, target) { Label = ReferenceLabel, Weight = 1 });
                    if (assembly.Origin == AssemblyOrigin.Project)
                    {
                        fanIn[target]++;
                    }
                }
            }

            int projectScripts = 0;
            int outsideScripts = 0;
            int projectAssemblies = 0;
            int pluginAssemblies = 0;
            int maxFanIn = 0;
            foreach (AssemblyDescription assembly in sorted)
            {
                if (assembly.Origin == AssemblyOrigin.Plugin)
                {
                    pluginAssemblies++;
                }
                else if (assembly.Origin == AssemblyOrigin.Project)
                {
                    projectAssemblies++;
                    projectScripts += assembly.SourceFileCount;
                    if (assembly.IsOutsideDefinition)
                    {
                        outsideScripts += assembly.SourceFileCount;
                    }
                }

                int references = fanIn[assembly.Name];
                maxFanIn = Math.Max(maxFanIn, references);
                GraphNode node = nodes[assembly.Name];
                node.Weight = references + assembly.SourceFileCount / (double)ScriptsPerWeightPoint;
                node.Sublabel = Plural(assembly.SourceFileCount, "script", "scripts") + " · used by " + references.ToString(CultureInfo.InvariantCulture);
            }

            report.Metrics.Add(new Metric("assemblies.count", Measurement.Count(sorted.Count), Budget.None, "Assemblies"));
            report.Metrics.Add(new Metric("assemblies.project", Measurement.Count(projectAssemblies), Budget.None, "Project assemblies"));
            report.Metrics.Add(new Metric("assemblies.plugins", Measurement.Count(pluginAssemblies), Budget.None, "Plugin assemblies"));
            report.Metrics.Add(new Metric("assemblies.packages", Measurement.Count(sorted.Count - projectAssemblies - pluginAssemblies), Budget.None, "Package assemblies"));
            report.Metrics.Add(new Metric("scripts.project", Measurement.Count(projectScripts), Budget.None, "Project scripts"));
            report.Metrics.Add(new Metric("scripts.outside-definition", Measurement.Count(outsideScripts), Budget.None, "Project scripts outside any assembly definition"));
            report.Metrics.Add(new Metric("scripts.outside-definition-share", Measurement.Percent(Share(outsideScripts, projectScripts)), Budget.None, "Share of project scripts outside any assembly definition"));
            report.Metrics.Add(new Metric("references.count", Measurement.Count(report.Graph.Edges.Count), Budget.None, "References"));
            report.Metrics.Add(new Metric("assemblies.max-fan-in", Measurement.Count(maxFanIn), Budget.None, "Highest fan-in from project assemblies"));

            foreach (AssemblyDescription assembly in sorted)
            {
                if (assembly.Origin != AssemblyOrigin.Project)
                {
                    continue;
                }

                if (assembly.IsOutsideDefinition)
                {
                    if (assembly.SourceFileCount > 0)
                    {
                        report.Findings.Add(OutsideDefinitionFinding(assembly, projectScripts));
                    }

                    continue;
                }

                int references = fanIn[assembly.Name];
                if (references >= HubFanIn)
                {
                    report.Findings.Add(HubFinding(assembly, references));
                }

                if (assembly.SourceFileCount >= LargeSourceFiles)
                {
                    report.Findings.Add(LargeFinding(assembly));
                }
            }

            report.Notes.Add("Assemblies come from the Editor's compilation pipeline, so one excluded from the Editor platform does not appear, and the defines of the active build target apply.");
            if (ignoredReferences > 0)
            {
                report.Notes.Add(Plural(ignoredReferences, "reference", "references") + " to assemblies outside the scan " + (ignoredReferences == 1 ? "was" : "were") + " ignored.");
            }

            report.Notes.Add("Kinds are Runtime, Editor, Test and External (anything under Packages/ or Assets/Plugins/) for now; the Data, Service and Messaging kinds and the list of third-party folders arrive with the project settings rules.");
            return report;
        }

        private static NodeKind KindOf(AssemblyDescription assembly)
        {
            if (assembly.Origin != AssemblyOrigin.Project)
            {
                return NodeKind.External;
            }

            if (assembly.IsTest)
            {
                return NodeKind.Test;
            }

            return assembly.IsEditorOnly ? NodeKind.Editor : NodeKind.Runtime;
        }

        private static string GroupOf(AssemblyDescription assembly, NodeKind kind)
        {
            if (assembly.Origin == AssemblyOrigin.Plugin)
            {
                return "Plugins";
            }

            switch (kind)
            {
                case NodeKind.External:
                    return "Packages";
                case NodeKind.Test:
                    return "Tests";
                case NodeKind.Editor:
                    return "Editor";
                default:
                    return "Runtime";
            }
        }

        private static Finding OutsideDefinitionFinding(AssemblyDescription assembly, int projectScripts)
        {
            double share = Share(assembly.SourceFileCount, projectScripts);
            Severity severity = share >= 50 ? Severity.Warning : share >= 10 ? Severity.Advice : Severity.Info;
            var finding = new Finding(OutsideDefinitionCheck, assembly.Name)
            {
                Title = Plural(assembly.SourceFileCount, "script compiles", "scripts compile") + " outside any assembly definition (" + share.ToString("0.#", CultureInfo.InvariantCulture) + " % of the project)",
                Severity = severity,
                Budget = Budget.None,
                Measured = Measurement.Count(assembly.SourceFileCount),
                Verdict = severity > Severity.Info ? "Move them into assembly definitions so a change recompiles one assembly instead of the whole project" : "",
            };
            return finding;
        }

        private static Finding HubFinding(AssemblyDescription assembly, int references)
        {
            var finding = new Finding(HubCheck, assembly.Name)
            {
                Title = "Referenced by " + Plural(references, "project assembly", "project assemblies") + "; a change here recompiles all of them",
                Severity = Severity.Info,
                Budget = Budget.None,
                Measured = Measurement.Count(references),
            };
            finding.Evidence.Add(new Evidence(assembly.DefinitionPath, 0, 0, "asmdef"));
            return finding;
        }

        private static Finding LargeFinding(AssemblyDescription assembly)
        {
            var finding = new Finding(LargeCheck, assembly.Name)
            {
                Title = Plural(assembly.SourceFileCount, "script recompiles", "scripts recompile") + " on any change inside this assembly",
                Severity = Severity.Advice,
                Budget = Budget.None,
                Measured = Measurement.Count(assembly.SourceFileCount),
                Verdict = "Split it along feature seams so a change recompiles a smaller assembly",
            };
            finding.Evidence.Add(new Evidence(assembly.DefinitionPath, 0, 0, "asmdef"));
            return finding;
        }

        private static double Share(int part, int whole)
        {
            return whole == 0 ? 0 : 100.0 * part / whole;
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}
