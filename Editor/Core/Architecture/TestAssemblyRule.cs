using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Decides whether an assembly definition describes a test assembly, from the file rather than from the
    /// compiled references: an assembly that does not override its references gets every precompiled DLL
    /// in the project, NUnit included, so the DLL list says nothing. The file does, in one of three ways
    /// Unity has used over the years.
    /// </summary>
    internal static class TestAssemblyRule
    {
        public const string IncludeTestsConstraint = "UNITY_INCLUDE_TESTS";
        public const string TestAssembliesOption = "TestAssemblies";

        private static readonly string[] TestRunnerReferences =
        {
            "UnityEngine.TestRunner",
            "UnityEditor.TestRunner",
            "GUID:27619889b8ba8c24980f49ee34dbb44a",
            "GUID:0acc523941302664db1f4e527237feb3",
        };

        public static bool IsTest(AssemblyDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            foreach (string constraint in definition.DefineConstraints)
            {
                if (string.Equals(constraint, IncludeTestsConstraint, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (string option in definition.OptionalUnityReferences)
            {
                if (string.Equals(option, TestAssembliesOption, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            foreach (string reference in definition.References)
            {
                foreach (string testRunner in TestRunnerReferences)
                {
                    if (string.Equals(reference, testRunner, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
