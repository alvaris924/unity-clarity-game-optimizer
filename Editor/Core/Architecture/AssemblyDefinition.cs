using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>The fields of an .asmdef file the report cares about. Arrays are never null.</summary>
    internal sealed class AssemblyDefinition
    {
        public static readonly AssemblyDefinition Empty = new AssemblyDefinition(null, null, null);

        public AssemblyDefinition(string[] references, string[] defineConstraints, string[] optionalUnityReferences)
        {
            References = references ?? Array.Empty<string>();
            DefineConstraints = defineConstraints ?? Array.Empty<string>();
            OptionalUnityReferences = optionalUnityReferences ?? Array.Empty<string>();
        }

        /// <summary>Referenced assemblies by name, or as "GUID:..." when the file uses GUID references.</summary>
        public string[] References { get; }

        public string[] DefineConstraints { get; }

        /// <summary>The pre-2019 way of opting into the test framework: "TestAssemblies".</summary>
        public string[] OptionalUnityReferences { get; }
    }
}
