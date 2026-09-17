using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns the resolved packages, the plugin folders, the assembly name collisions and the scripting
    /// defines into the Dependencies report: one node per package grouped by source and one per plugin,
    /// edges for package dependencies, and findings for git packages that can move, packages resolved from
    /// a local path, direct packages nothing in the project's own definitions references, plugins whose
    /// scripts compile into the predefined assemblies, and names two files claim. Pure: the Editor side
    /// only supplies the descriptions.
    /// </summary>
    internal static class DependenciesReport
    {
        public const string Scope = "Project";
        public const string ManifestPath = "Packages/manifest.json";

        public const string GitFloatingCheck = "package.git-floating";
        public const string LocalCheck = "package.local";
        public const string UnreferencedCheck = "package.unreferenced";
        public const string CollisionCheck = "assembly.name-collision";
        public const string PluginOutsideDefinitionCheck = "plugin.outside-definition";

        private const string DependsLabel = "depends on";

        public static Report Build(IReadOnlyList<PackageDescription> packages, IReadOnlyList<PluginDescription> plugins, IReadOnlyList<AssemblyNameCollision> collisions, IReadOnlyList<string> defines)
        {
            if (packages == null)
            {
                throw new ArgumentNullException(nameof(packages));
            }

            plugins = plugins ?? Array.Empty<PluginDescription>();
            collisions = collisions ?? Array.Empty<AssemblyNameCollision>();
            defines = defines ?? Array.Empty<string>();

            var sortedPackages = new List<PackageDescription>(packages);
            sortedPackages.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            var sortedPlugins = new List<PluginDescription>(plugins);
            sortedPlugins.Sort((a, b) => string.CompareOrdinal(a.Folder, b.Folder));

            var report = new Report(Area.Dependencies, Scope);
            var nodes = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
            var dependants = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (PackageDescription package in sortedPackages)
            {
                if (nodes.ContainsKey(package.Name))
                {
                    continue;
                }

                var node = new GraphNode(package.Name, package.DisplayName, NodeKind.External) { Group = GroupOf(package.Source) };
                node.Evidence.Add(new Evidence(package.AssetPath + "/package.json", 0, 0, "package"));
                if (package.IsDirect)
                {
                    node.Evidence.Add(new Evidence(ManifestPath, 0, 0, "manifest"));
                }

                nodes.Add(package.Name, report.Graph.AddNode(node));
                dependants.Add(package.Name, 0);
            }

            int ignoredDependencies = 0;
            foreach (PackageDescription package in sortedPackages)
            {
                var targets = new List<string>(package.Dependencies);
                targets.Sort(string.CompareOrdinal);
                string previous = null;
                foreach (string target in targets)
                {
                    if (target == previous || target == package.Name)
                    {
                        continue;
                    }

                    previous = target;
                    if (!nodes.ContainsKey(target))
                    {
                        ignoredDependencies++;
                        continue;
                    }

                    report.Graph.AddEdge(new GraphEdge(package.Name, target) { Label = DependsLabel, Weight = 1 });
                    dependants[target]++;
                }
            }

            int direct = 0;
            int registry = 0;
            int git = 0;
            int local = 0;
            int builtIn = 0;
            int floating = 0;
            long bytes = 0;
            foreach (PackageDescription package in sortedPackages)
            {
                if (package.IsDirect)
                {
                    direct++;
                }

                switch (package.Source)
                {
                    case PackageSourceKind.Registry:
                        registry++;
                        break;
                    case PackageSourceKind.Git:
                        git++;
                        break;
                    case PackageSourceKind.BuiltIn:
                        builtIn++;
                        break;
                    case PackageSourceKind.Local:
                    case PackageSourceKind.Embedded:
                    case PackageSourceKind.Tarball:
                        local++;
                        break;
                }

                if (package.Source == PackageSourceKind.Git && !package.IsGitPinnedToCommit)
                {
                    floating++;
                }

                if (package.Bytes > 0)
                {
                    bytes += package.Bytes;
                }

                int users = dependants[package.Name];
                GraphNode node = nodes[package.Name];
                node.Weight = package.ProjectReferrers + users + (package.IsDirect ? 1 : 0);
                node.Sublabel = VersionLabel(package) + " · used by " + package.ProjectReferrers.ToString(CultureInfo.InvariantCulture);
            }

            int pluginScripts = 0;
            int pluginOutside = 0;
            int libraries = 0;
            foreach (PluginDescription plugin in sortedPlugins)
            {
                pluginScripts += plugin.ScriptCount;
                pluginOutside += plugin.ScriptsOutsideDefinitions;
                libraries += plugin.LibraryCount;
                if (nodes.ContainsKey(plugin.Folder))
                {
                    continue;
                }

                var node = new GraphNode(plugin.Folder, plugin.Name, NodeKind.External)
                {
                    Group = "Plugins",
                    Weight = plugin.DefinitionCount + plugin.LibraryCount + plugin.ScriptCount / 50.0,
                    Sublabel = PluginLabel(plugin),
                };
                node.Evidence.Add(new Evidence(plugin.Folder, 0, 0, "folder"));
                nodes.Add(plugin.Folder, report.Graph.AddNode(node));
            }

            report.Metrics.Add(new Metric("packages.count", Measurement.Count(sortedPackages.Count), Budget.None, "Packages"));
            report.Metrics.Add(new Metric("packages.direct", Measurement.Count(direct), Budget.None, "Named in the manifest"));
            report.Metrics.Add(new Metric("packages.registry", Measurement.Count(registry), Budget.None, "From a registry"));
            report.Metrics.Add(new Metric("packages.git", Measurement.Count(git), Budget.None, "From git"));
            report.Metrics.Add(new Metric("packages.git-floating", Measurement.Count(floating), Budget.None, "Git packages not pinned to a commit"));
            report.Metrics.Add(new Metric("packages.local", Measurement.Count(local), Budget.None, "Local, embedded or tarball"));
            report.Metrics.Add(new Metric("packages.built-in", Measurement.Count(builtIn), Budget.None, "Built-in modules"));
            report.Metrics.Add(new Metric("packages.bytes", Measurement.Bytes(bytes), Budget.None, "Packages on disk"));
            report.Metrics.Add(new Metric("plugins.count", Measurement.Count(sortedPlugins.Count), Budget.None, "Plugins in third-party folders"));
            report.Metrics.Add(new Metric("plugins.libraries", Measurement.Count(libraries), Budget.None, "Precompiled libraries in plugins"));
            report.Metrics.Add(new Metric("plugins.scripts", Measurement.Count(pluginScripts), Budget.None, "Plugin scripts"));
            report.Metrics.Add(new Metric("plugins.scripts-outside-definition", Measurement.Count(pluginOutside), Budget.None, "Plugin scripts outside any assembly definition"));
            report.Metrics.Add(new Metric("assemblies.name-collisions", Measurement.Count(collisions.Count), Budget.None, "Assembly names claimed twice"));
            report.Metrics.Add(new Metric("defines.count", Measurement.Count(defines.Count), Budget.None, "Scripting defines, active target"));

            foreach (PackageDescription package in sortedPackages)
            {
                if (package.Source == PackageSourceKind.Git && !package.IsGitPinnedToCommit)
                {
                    report.Findings.Add(GitFloatingFinding(package));
                }

                if (package.Source == PackageSourceKind.Local || package.Source == PackageSourceKind.Tarball)
                {
                    report.Findings.Add(LocalFinding(package));
                }

                if (package.IsDirect && package.ProjectReferrers == 0 && (package.Source == PackageSourceKind.Registry || package.Source == PackageSourceKind.Git))
                {
                    report.Findings.Add(UnreferencedFinding(package));
                }
            }

            foreach (PluginDescription plugin in sortedPlugins)
            {
                if (plugin.ScriptsOutsideDefinitions > 0)
                {
                    report.Findings.Add(PluginOutsideDefinitionFinding(plugin));
                }
            }

            var sortedCollisions = new List<AssemblyNameCollision>(collisions);
            sortedCollisions.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            foreach (AssemblyNameCollision collision in sortedCollisions)
            {
                report.Findings.Add(CollisionFinding(collision));
            }

            report.Notes.Add("A package's referrers are the project's own assembly definitions that reference one of its assemblies; the predefined assemblies reference every package automatically, so scripts outside assembly definitions may use a package that shows no referrer.");
            report.Notes.Add("Package sizes are the resolved folders on disk, a footprint on the machine and not a build size; a build ships only what it references.");
            if (ignoredDependencies > 0)
            {
                report.Notes.Add(Plural(ignoredDependencies, "dependency", "dependencies") + " on packages outside the scan " + (ignoredDependencies == 1 ? "was" : "were") + " ignored.");
            }

            if (defines.Count > 0)
            {
                report.Notes.Add("Scripting defines for the active build target: " + string.Join(", ", defines) + ".");
            }

            return report;
        }

        public static string GroupOf(PackageSourceKind source)
        {
            switch (source)
            {
                case PackageSourceKind.Registry:
                    return "Registry";
                case PackageSourceKind.BuiltIn:
                    return "Built-in";
                case PackageSourceKind.Embedded:
                    return "Embedded";
                case PackageSourceKind.Local:
                    return "Local";
                case PackageSourceKind.Git:
                    return "Git";
                case PackageSourceKind.Tarball:
                    return "Tarball";
                default:
                    return "Unknown";
            }
        }

        private static string VersionLabel(PackageDescription package)
        {
            if (package.Source == PackageSourceKind.Git)
            {
                string reference = package.IsGitPinnedToCommit ? package.ShortHash : (package.GitRevision.Length > 0 ? package.GitRevision : "default branch");
                return reference + (package.GitHash.Length > 0 && !package.IsGitPinnedToCommit ? " @" + package.ShortHash : "");
            }

            return package.Version.Length > 0 ? "v" + package.Version : GroupOf(package.Source).ToLowerInvariant();
        }

        private static string PluginLabel(PluginDescription plugin)
        {
            string label = Plural(plugin.DefinitionCount, "asmdef", "asmdefs") + " · " + Plural(plugin.LibraryCount, "DLL", "DLLs") + " · " + Plural(plugin.ScriptCount, "script", "scripts");
            return plugin.ScriptsOutsideDefinitions > 0 ? label + ", " + plugin.ScriptsOutsideDefinitions.ToString(CultureInfo.InvariantCulture) + " outside" : label;
        }

        private static Finding GitFloatingFinding(PackageDescription package)
        {
            bool tag = package.IsGitOnVersionTag;
            var finding = new Finding(GitFloatingCheck, package.Name)
            {
                Title = tag
                    ? "Follows the git tag " + package.GitRevision + "; a tag can be moved, and this checkout resolved it to " + package.ShortHash
                    : "Follows " + (package.GitRevision.Length > 0 ? "the git branch " + package.GitRevision : "the default git branch") + "; every fresh checkout can differ, this one resolved to " + package.ShortHash,
                Severity = tag ? Severity.Advice : Severity.Warning,
                Budget = Budget.None,
                Verdict = "Pin the manifest to the commit: #" + package.GitHash,
            };
            finding.Evidence.Add(new Evidence(ManifestPath, 0, 0, "manifest"));
            return finding;
        }

        private static Finding LocalFinding(PackageDescription package)
        {
            var finding = new Finding(LocalCheck, package.Name)
            {
                Title = "Resolved from a local " + (package.Source == PackageSourceKind.Tarball ? "tarball" : "path") + "; other machines need the same file in the same place",
                Severity = Severity.Info,
                Budget = Budget.None,
            };
            finding.Evidence.Add(new Evidence(ManifestPath, 0, 0, "manifest"));
            return finding;
        }

        private static Finding UnreferencedFinding(PackageDescription package)
        {
            var finding = new Finding(UnreferencedCheck, package.Name)
            {
                Title = "Named in the manifest, but no assembly definition of the project references it; scripts outside assembly definitions may still use it",
                Severity = Severity.Info,
                Budget = Budget.None,
            };
            finding.Evidence.Add(new Evidence(ManifestPath, 0, 0, "manifest"));
            return finding;
        }

        private static Finding PluginOutsideDefinitionFinding(PluginDescription plugin)
        {
            var finding = new Finding(PluginOutsideDefinitionCheck, plugin.Folder)
            {
                Title = plugin.ScriptsOutsideDefinitions.ToString(CultureInfo.InvariantCulture) + " of " + Plural(plugin.ScriptCount, "script", "scripts") + (plugin.ScriptsOutsideDefinitions == 1 ? " compiles" : " compile") + " into the predefined assemblies with the project's own code",
                Severity = Severity.Advice,
                Budget = Budget.None,
                Measured = Measurement.Count(plugin.ScriptsOutsideDefinitions),
                Verdict = "Add an assembly definition at the plugin's root so a change elsewhere does not recompile it, and a change in it recompiles only itself",
            };
            finding.Evidence.Add(new Evidence(plugin.Folder, 0, 0, "folder"));
            return finding;
        }

        private static Finding CollisionFinding(AssemblyNameCollision collision)
        {
            var finding = new Finding(CollisionCheck, collision.Name)
            {
                Title = Plural(collision.Paths.Count, "file claims", "files claim") + " the assembly name " + collision.Name + "; Unity warns of hard-to-diagnose failures and crashes",
                Severity = Severity.Warning,
                Budget = Budget.None,
                Measured = Measurement.Count(collision.Paths.Count),
                Verdict = "Rename one of them, or remove the copy that is not needed",
            };
            foreach (string path in collision.Paths)
            {
                finding.Evidence.Add(new Evidence(path, 0, 0, path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ? "library" : "asmdef"));
            }

            return finding;
        }

        private static string Plural(int count, string singular, string plural)
        {
            return count.ToString(CultureInfo.InvariantCulture) + " " + (count == 1 ? singular : plural);
        }
    }
}
