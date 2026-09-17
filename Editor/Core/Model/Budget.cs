namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Which budget a number belongs to. Download size and runtime RAM are different budgets, and most
    /// intuitive fixes move only one of them: mesh and crunch compression shrink the download and leave
    /// RAM untouched, turning Read/Write off frees RAM and changes nothing on disk. So every metric and
    /// finding names its budget, and a report never puts two size budgets in one table.
    /// </summary>
    internal enum Budget
    {
        /// <summary>A count or a fact with no size or time attached.</summary>
        None = 0,

        /// <summary>Bytes in the player build as downloaded and installed.</summary>
        Download = 1,

        /// <summary>Bytes the device holds while the game runs.</summary>
        RuntimeRam = 2,

        /// <summary>Milliseconds per frame in a running player.</summary>
        FrameTime = 3,

        /// <summary>Seconds the team waits: compile, domain reload, entering Play mode.</summary>
        IterationTime = 4,
    }

    internal static class Budgets
    {
        /// <summary>The name shown to people.</summary>
        public static string Describe(Budget budget)
        {
            switch (budget)
            {
                case Budget.Download:
                    return "Download size";
                case Budget.RuntimeRam:
                    return "Runtime RAM";
                case Budget.FrameTime:
                    return "Frame time";
                case Budget.IterationTime:
                    return "Iteration time";
                default:
                    return "None";
            }
        }
    }
}
