using System;
using System.Collections.Generic;
using System.IO;
using ClarityGameOptimizer.Core;
using ClarityGameOptimizer.Settings;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads what the project pulled in: every resolved package from the Package Manager, the project
    /// assembly definitions that reference each one, the plugins in the third-party folders with their
    /// scripts and libraries, the assembly names two files claim, and the scripting defines of the active
    /// build target. Everything that decides what the report says lives in <see cref="DependenciesReport"/>.
    /// </summary>
    internal sealed class DependenciesAnalyzer
    {
        private const string PackagesPrefix = "Packages/";
        private const string AssetsPrefix = "Assets/";
        private const string PluginsFolderName = "Plugins";
        private const string SessionKeyPrefix = "ClarityGameOptimizer.PackageBytes.";

        private static readonly HashSet<string> PredefinedAssemblies = new HashSet<string>(StringComparer.Ordinal)
        {
            "Assembly-CSharp", "Assembly-CSharp-Editor", "Assembly-CSharp-firstpass", "Assembly-CSharp-Editor-firstpass",
        };

        public Report Scan()
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;
            string projectRoot = ReportFolders.ProjectRoot;
            PackageInfo[] registered = PackageInfo.GetAllRegisteredPackages();
            Assembly[] assemblies = CompilationPipeline.GetAssemblies();
            string[] libraries = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.UserAssembly);

            List<PackageDescription> packages = DescribePackages(registered, assemblies, libraries, settings.ThirdPartyFolders, projectRoot);
            List<PluginDescription> plugins = DescribePlugins(settings.ThirdPartyFolders, projectRoot);
            List<AssemblyNameCollision> collisions = FindCollisions(assemblies, libraries, registered, projectRoot);
            List<string> defines = Defines();

            Report report = DependenciesReport.Build(packages, plugins, collisions, defines);
            ReportEnvironment.Stamp(report);
            return report;
        }

        internal static List<PackageDescription> DescribePackages(PackageInfo[] registered, Assembly[] assemblies, string[] libraries, IReadOnlyList<string> thirdPartyFolders, string projectRoot)
        {
            var packages = new List<PackageDescription>(registered.Length);
            var byName = new Dictionary<string, PackageDescription>(StringComparer.Ordinal);
            foreach (PackageInfo info in registered)
            {
                var package = new PackageDescription(info.name)
                {
                    DisplayName = info.displayName,
                    Version = info.version,
                    Source = SourceOf(info.source),
                    IsDirect = info.isDirectDependency,
                    GitRevision = info.git != null ? info.git.revision : "",
                    GitHash = info.git != null ? info.git.hash : "",
                    Bytes = PackageBytes(info),
                };
                foreach (DependencyInfo dependency in info.dependencies)
                {
                    package.Dependencies.Add(dependency.name);
                }

                packages.Add(package);
                byName[package.Name] = package;
            }

            CountReferrers(byName, assemblies, registered, thirdPartyFolders, projectRoot);
            return packages;
        }

        /// <summary>Every project assembly definition that references a package assembly, or a precompiled library inside a package, counts once for that package.</summary>
        private static void CountReferrers(Dictionary<string, PackageDescription> packages, Assembly[] assemblies, PackageInfo[] registered, IReadOnlyList<string> thirdPartyFolders, string projectRoot)
        {
            var definitionPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Assembly assembly in assemblies)
            {
                definitionPaths[assembly.name] = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name) ?? "";
            }

            var counted = new HashSet<string>(StringComparer.Ordinal);
            foreach (Assembly assembly in assemblies)
            {
                string definition = definitionPaths[assembly.name];
                if (definition.Length == 0 || !definition.StartsWith(AssetsPrefix, StringComparison.Ordinal) || FolderRules.IsUnderAny(definition, thirdPartyFolders))
                {
                    continue;
                }

                foreach (Assembly reference in assembly.assemblyReferences)
                {
                    Count(packages, counted, assembly.name, PackageOf(definitionPaths[reference.name], registered, projectRoot));
                }

                foreach (string library in assembly.compiledAssemblyReferences)
                {
                    Count(packages, counted, assembly.name, PackageOf(library, registered, projectRoot));
                }
            }
        }

        private static void Count(Dictionary<string, PackageDescription> packages, HashSet<string> counted, string referrer, string packageName)
        {
            PackageDescription package;
            if (packageName != null && packages.TryGetValue(packageName, out package) && counted.Add(referrer + "\n" + packageName))
            {
                package.ProjectReferrers++;
            }
        }

        /// <summary>The package a path belongs to: by its Packages/ prefix, or by the resolved folder it lies under; null for anything else.</summary>
        internal static string PackageOf(string path, PackageInfo[] registered, string projectRoot)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith(PackagesPrefix, StringComparison.Ordinal))
            {
                int end = normalized.IndexOf('/', PackagesPrefix.Length);
                return end < 0 ? normalized.Substring(PackagesPrefix.Length) : normalized.Substring(PackagesPrefix.Length, end - PackagesPrefix.Length);
            }

            if (normalized.StartsWith(AssetsPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            string full = Path.IsPathRooted(normalized) ? normalized : Path.Combine(projectRoot, normalized).Replace('\\', '/');
            foreach (PackageInfo info in registered)
            {
                if (!string.IsNullOrEmpty(info.resolvedPath) && FolderRules.IsUnder(full, info.resolvedPath.Replace('\\', '/')))
                {
                    return info.name;
                }
            }

            return null;
        }

        /// <summary>A folder named Plugins holds one plugin per subfolder; any other third-party folder is one plugin.</summary>
        internal static List<PluginDescription> DescribePlugins(IReadOnlyList<string> thirdPartyFolders, string projectRoot)
        {
            var plugins = new List<PluginDescription>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in thirdPartyFolders)
            {
                string full = Path.Combine(projectRoot, folder);
                if (!Directory.Exists(full))
                {
                    continue;
                }

                if (string.Equals(Path.GetFileName(folder.TrimEnd('/')), PluginsFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    var children = new List<string>(Directory.GetDirectories(full));
                    children.Sort(string.CompareOrdinal);
                    foreach (string child in children)
                    {
                        string relative = folder.TrimEnd('/') + "/" + Path.GetFileName(child);
                        if (seen.Add(relative))
                        {
                            plugins.Add(DescribePlugin(relative, child, projectRoot));
                        }
                    }
                }
                else if (seen.Add(folder))
                {
                    plugins.Add(DescribePlugin(folder, full, projectRoot));
                }
            }

            return plugins;
        }

        private static PluginDescription DescribePlugin(string relativeFolder, string fullFolder, string projectRoot)
        {
            var plugin = new PluginDescription(relativeFolder) { Bytes = DirectorySize(fullFolder) };
            foreach (string file in EnumerateFiles(fullFolder))
            {
                string extension = Path.GetExtension(file);
                if (string.Equals(extension, ".asmdef", StringComparison.OrdinalIgnoreCase))
                {
                    plugin.DefinitionCount++;
                }
                else if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
                {
                    plugin.LibraryCount++;
                }
                else if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
                {
                    plugin.ScriptCount++;
                    string relative = file.Substring(projectRoot.Length).TrimStart('\\', '/').Replace('\\', '/');
                    string assembly = CompilationPipeline.GetAssemblyNameFromScriptPath(relative) ?? "";
                    if (assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        assembly = assembly.Substring(0, assembly.Length - 4);
                    }

                    if (PredefinedAssemblies.Contains(assembly))
                    {
                        plugin.ScriptsOutsideDefinitions++;
                    }
                }
            }

            return plugin;
        }

        /// <summary>Assembly names claimed by more than one file: definitions from the pipeline plus every precompiled library in the project.</summary>
        internal static List<AssemblyNameCollision> FindCollisions(Assembly[] assemblies, string[] libraries, PackageInfo[] registered, string projectRoot)
        {
            var paths = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (Assembly assembly in assemblies)
            {
                string definition = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assembly.name);
                if (!string.IsNullOrEmpty(definition))
                {
                    Claim(paths, assembly.name, definition);
                }
            }

            foreach (string library in libraries)
            {
                Claim(paths, Path.GetFileNameWithoutExtension(library), DisplayPath(library, registered, projectRoot));
            }

            var collisions = new List<AssemblyNameCollision>();
            foreach (KeyValuePair<string, List<string>> entry in paths)
            {
                if (entry.Value.Count > 1)
                {
                    entry.Value.Sort(string.CompareOrdinal);
                    collisions.Add(new AssemblyNameCollision(entry.Key, entry.Value));
                }
            }

            collisions.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            return collisions;
        }

        private static void Claim(Dictionary<string, List<string>> paths, string name, string path)
        {
            List<string> list;
            if (!paths.TryGetValue(name, out list))
            {
                list = new List<string>();
                paths.Add(name, list);
            }

            string normalized = path.Replace('\\', '/');
            if (!list.Contains(normalized))
            {
                list.Add(normalized);
            }
        }

        /// <summary>A path as the asset database would show it: a library in the package cache reads as Packages/&lt;name&gt;/....</summary>
        internal static string DisplayPath(string path, PackageInfo[] registered, string projectRoot)
        {
            string normalized = path.Replace('\\', '/');
            if (!Path.IsPathRooted(normalized))
            {
                return normalized;
            }

            // The package cache lives under the project, and a local package can contain the project, so the deepest folder that holds the path decides.
            string root = projectRoot.Replace('\\', '/').TrimEnd('/');
            string bestFolder = FolderRules.IsUnder(normalized, root) ? root : null;
            string bestPrefix = bestFolder == null ? null : "";
            foreach (PackageInfo info in registered)
            {
                if (string.IsNullOrEmpty(info.resolvedPath))
                {
                    continue;
                }

                string resolved = info.resolvedPath.Replace('\\', '/').TrimEnd('/');
                if (FolderRules.IsUnder(normalized, resolved) && (bestFolder == null || resolved.Length > bestFolder.Length))
                {
                    bestFolder = resolved;
                    bestPrefix = PackagesPrefix + info.name;
                }
            }

            if (bestFolder == null)
            {
                return normalized;
            }

            string rest = normalized.Substring(bestFolder.Length).TrimStart('/');
            return bestPrefix.Length == 0 ? rest : bestPrefix + "/" + rest;
        }

        internal static List<string> Defines()
        {
            NamedBuildTarget target = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            var defines = new List<string>();
            foreach (string define in (PlayerSettings.GetScriptingDefineSymbols(target) ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = define.Trim();
                if (trimmed.Length > 0)
                {
                    defines.Add(trimmed);
                }
            }

            return defines;
        }

        internal static PackageSourceKind SourceOf(PackageSource source)
        {
            switch (source)
            {
                case PackageSource.Registry:
                    return PackageSourceKind.Registry;
                case PackageSource.BuiltIn:
                    return PackageSourceKind.BuiltIn;
                case PackageSource.Embedded:
                    return PackageSourceKind.Embedded;
                case PackageSource.Local:
                    return PackageSourceKind.Local;
                case PackageSource.Git:
                    return PackageSourceKind.Git;
                case PackageSource.LocalTarball:
                    return PackageSourceKind.Tarball;
                default:
                    return PackageSourceKind.Unknown;
            }
        }

        /// <summary>
        /// The size of a package's resolved folder. Registry and git packages sit in content-addressed cache
        /// folders that never change, so their size is measured once per Editor session and kept in session
        /// state; local, embedded and tarball packages are walked every time; built-in modules have no folder worth measuring.
        /// </summary>
        internal static long PackageBytes(PackageInfo info)
        {
            if (info.source == PackageSource.BuiltIn || string.IsNullOrEmpty(info.resolvedPath))
            {
                return -1;
            }

            bool immutable = info.source == PackageSource.Registry || info.source == PackageSource.Git;
            string key = immutable ? SessionKeyPrefix + info.resolvedPath : null;
            if (key != null)
            {
                string cached = SessionState.GetString(key, "");
                long bytes;
                if (cached.Length > 0 && long.TryParse(cached, out bytes))
                {
                    return bytes;
                }
            }

            long measured = DirectorySize(info.resolvedPath);
            if (key != null && measured >= 0)
            {
                SessionState.SetString(key, measured.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }

            return measured;
        }

        /// <summary>The bytes of every file under a folder, skipping .git folders; -1 when the folder is missing or unreadable.</summary>
        internal static long DirectorySize(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                return -1;
            }

            long bytes = 0;
            foreach (string file in EnumerateFiles(folder))
            {
                try
                {
                    bytes += new FileInfo(file).Length;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }

            return bytes;
        }

        private static IEnumerable<string> EnumerateFiles(string folder)
        {
            var pending = new Stack<string>();
            pending.Push(folder);
            while (pending.Count > 0)
            {
                string current = pending.Pop();
                string[] files;
                string[] directories;
                try
                {
                    files = Directory.GetFiles(current);
                    directories = Directory.GetDirectories(current);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                foreach (string file in files)
                {
                    yield return file;
                }

                foreach (string directory in directories)
                {
                    if (!string.Equals(Path.GetFileName(directory), ".git", StringComparison.OrdinalIgnoreCase))
                    {
                        pending.Push(directory);
                    }
                }
            }
        }
    }
}
