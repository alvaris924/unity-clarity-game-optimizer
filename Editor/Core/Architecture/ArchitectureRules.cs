using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>The thresholds the Architecture checks use. The defaults are what the checks shipped with; the project settings can move them.</summary>
    internal sealed class ArchitectureRules
    {
        /// <summary>Fan-in from the project's own assemblies at which an assembly counts as a hub worth knowing about.</summary>
        public const int DefaultHubFanIn = 5;

        /// <summary>Source files from which a project assembly counts as large.</summary>
        public const int DefaultLargeSourceFiles = 250;

        public static readonly ArchitectureRules Default = new ArchitectureRules(DefaultHubFanIn, DefaultLargeSourceFiles);

        public ArchitectureRules(int hubFanIn, int largeSourceFiles)
        {
            if (hubFanIn < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(hubFanIn), "A hub needs at least one referrer.");
            }

            if (largeSourceFiles < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(largeSourceFiles), "A large assembly needs at least one script.");
            }

            HubFanIn = hubFanIn;
            LargeSourceFiles = largeSourceFiles;
        }

        public int HubFanIn { get; }

        public int LargeSourceFiles { get; }
    }
}
