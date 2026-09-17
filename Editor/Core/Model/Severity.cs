namespace ClarityGameOptimizer.Core
{
    /// <summary>How much a finding matters. A higher value is more severe, so sorting descending puts the urgent rows first.</summary>
    internal enum Severity
    {
        /// <summary>A fact worth knowing; nothing to do.</summary>
        Info = 0,

        /// <summary>An improvement that is safe to skip.</summary>
        Advice = 1,

        /// <summary>A real cost with a known fix; worth looking at.</summary>
        Warning = 2,

        /// <summary>Blocks a goal such as a memory budget or a build; fix first.</summary>
        Critical = 3,
    }
}
