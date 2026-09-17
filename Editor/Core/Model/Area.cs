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
    }
}
