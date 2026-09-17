using System.Collections.Generic;
using ClarityGameOptimizer.Core;

namespace ClarityGameOptimizer.Tests.Core
{
    /// <summary>A small project the Architecture tests and the diagram fixtures share.</summary>
    internal static class ArchitectureFixtures
    {
        /// <summary>Ten assemblies: a game with an events hub, an editor and a test assembly, two predefined ones, a plugin and two packages.</summary>
        public static List<AssemblyDescription> TenAssemblies()
        {
            return new List<AssemblyDescription>
            {
                Describe("Game.Runtime", "Assets/Game/Game.Runtime.asmdef", 312, AssemblyOrigin.Project, false, false, "Game.Events", "PlayFab.Wrappers", "UniTask", "Unity.TextMeshPro"),
                Describe("Game.Editor", "Assets/Game/Editor/Game.Editor.asmdef", 40, AssemblyOrigin.Project, true, false, "Game.Runtime", "Game.Events", "UniTask"),
                Describe("Game.Tests", "Assets/Tests/Game.Tests.asmdef", 12, AssemblyOrigin.Project, true, true, "Game.Runtime", "Game.Events", "UniTask", "UnityEngine.TestRunner"),
                Describe("Game.Events", "Assets/Game/Events/Game.Events.asmdef", 30, AssemblyOrigin.Project, false, false),
                Describe("PlayFab.Wrappers", "Assets/PlayFab/PlayFab.Wrappers.asmdef", 14, AssemblyOrigin.Project, false, false, "Game.Events", "UniTask"),
                Describe("Assembly-CSharp", "", 500, AssemblyOrigin.Project, false, false, "Game.Runtime", "Game.Events", "UniTask", "Unity.TextMeshPro"),
                Describe("Assembly-CSharp-Editor", "", 20, AssemblyOrigin.Project, true, false, "Assembly-CSharp"),
                Describe("UniTask", "Assets/Plugins/UniTask/Runtime/UniTask.asmdef", 76, AssemblyOrigin.Plugin, false, false),
                Describe("Unity.TextMeshPro", "Packages/com.unity.textmeshpro/Scripts/Runtime/Unity.TextMeshPro.asmdef", 90, AssemblyOrigin.Package, false, false),
                Describe("UnityEngine.TestRunner", "Packages/com.unity.test-framework/UnityEngine.TestRunner/UnityEngine.TestRunner.asmdef", 60, AssemblyOrigin.Package, false, true),
            };
        }

        /// <summary>The report those ten assemblies produce, stamped so it is the same on every machine.</summary>
        public static Report Report()
        {
            Report report = AssemblyGraphReport.Build(TenAssemblies());
            report.UnityVersion = "6000.2.9f1";
            report.BuildTarget = "Android";
            report.PackageVersion = "0.1.0";
            report.CreatedAt = ReportFixtures.Timestamp;
            return report;
        }

        private static AssemblyDescription Describe(string name, string definition, int scripts, AssemblyOrigin origin, bool editorOnly, bool test, params string[] references)
        {
            var description = new AssemblyDescription(name)
            {
                DefinitionPath = definition,
                SourceFileCount = scripts,
                Origin = origin,
                IsEditorOnly = editorOnly,
                IsTest = test,
            };
            description.References.AddRange(references);
            return description;
        }
    }
}
