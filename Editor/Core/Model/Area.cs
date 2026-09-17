namespace ClarityGameOptimizer.Core
{
    /// <summary>The seven areas a report can belong to. One analyzer run produces one report for one area.</summary>
    internal enum Area
    {
        Architecture = 0,
        Performance = 1,
        Memory = 2,
        BuildSize = 3,
        Assets = 4,
        Dependencies = 5,
        CodeQuality = 6,
    }

    internal static class Areas
    {
        /// <summary>The name shown to people, with spaces where the enum has none.</summary>
        public static string Describe(Area area)
        {
            switch (area)
            {
                case Area.BuildSize:
                    return "Build Size";
                case Area.CodeQuality:
                    return "Code Quality";
                default:
                    return area.ToString();
            }
        }

        /// <summary>The base name of the area's diagram files, which name what the nodes are.</summary>
        public static string DiagramBaseName(Area area)
        {
            switch (area)
            {
                case Area.Architecture:
                    return "assemblies";
                case Area.Dependencies:
                    return "packages";
                default:
                    return FolderName(area);
            }
        }

        /// <summary>The title of the area's diagram.</summary>
        public static string DiagramTitle(Area area)
        {
            switch (area)
            {
                case Area.Architecture:
                    return "Assembly map";
                case Area.Dependencies:
                    return "Package map";
                default:
                    return Describe(area) + " map";
            }
        }

        /// <summary>What one node of the area's graph is called, for counts.</summary>
        public static string NodeNoun(Area area, int count)
        {
            switch (area)
            {
                case Area.Architecture:
                    return count == 1 ? "assembly" : "assemblies";
                case Area.Dependencies:
                    return count == 1 ? "package or plugin" : "packages and plugins";
                default:
                    return count == 1 ? "node" : "nodes";
            }
        }

        /// <summary>The folder a report of this area is written to: lowercase, words joined by a hyphen.</summary>
        public static string FolderName(Area area)
        {
            switch (area)
            {
                case Area.BuildSize:
                    return "build-size";
                case Area.CodeQuality:
                    return "code-quality";
                default:
                    return area.ToString().ToLowerInvariant();
            }
        }
    }
}
