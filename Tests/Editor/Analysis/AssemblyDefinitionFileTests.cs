using ClarityGameOptimizer.Analysis;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Analysis
{
    public class AssemblyDefinitionFileTests
    {
        [Test]
        public void Parses_the_three_fields_the_rule_needs()
        {
            const string text = "{\n  \"name\": \"Game.Tests\",\n  \"references\": [\"Game.Runtime\", \"GUID:27619889b8ba8c24980f49ee34dbb44a\"],\n"
                + "  \"includePlatforms\": [\"Editor\"],\n  \"defineConstraints\": [\"UNITY_INCLUDE_TESTS\"],\n  \"optionalUnityReferences\": [\"TestAssemblies\"]\n}";

            AssemblyDefinition definition = AssemblyDefinitionFile.Parse(text);

            Assert.That(definition.References, Is.EqualTo(new[] { "Game.Runtime", "GUID:27619889b8ba8c24980f49ee34dbb44a" }));
            Assert.That(definition.DefineConstraints, Is.EqualTo(new[] { "UNITY_INCLUDE_TESTS" }));
            Assert.That(definition.OptionalUnityReferences, Is.EqualTo(new[] { "TestAssemblies" }));
        }

        [Test]
        public void Missing_fields_read_as_empty()
        {
            AssemblyDefinition definition = AssemblyDefinitionFile.Parse("{ \"name\": \"Game.Runtime\" }");

            Assert.That(definition.References, Is.Empty);
            Assert.That(definition.DefineConstraints, Is.Empty);
            Assert.That(definition.OptionalUnityReferences, Is.Empty);
        }

        [Test]
        public void Unreadable_text_and_paths_outside_assets_read_as_empty()
        {
            Assert.That(TestAssemblyRule.IsTest(AssemblyDefinitionFile.Parse("not json")), Is.False);
            Assert.That(AssemblyDefinitionFile.Read("").References, Is.Empty);
            Assert.That(AssemblyDefinitionFile.Read("Packages/com.unity.test-framework/UnityEngine.TestRunner/UnityEngine.TestRunner.asmdef").References, Is.Empty);
            Assert.That(AssemblyDefinitionFile.Read("Assets/Missing/Missing.asmdef").References, Is.Empty);
        }

        [Test]
        public void Origins_follow_the_folder_conventions()
        {
            Assert.That(AssemblyGraphAnalyzer.OriginOf("Packages/com.unity.textmeshpro/Scripts/Runtime/Unity.TextMeshPro.asmdef"), Is.EqualTo(AssemblyOrigin.Package));
            Assert.That(AssemblyGraphAnalyzer.OriginOf("Assets/Plugins/UniTask/Runtime/UniTask.asmdef"), Is.EqualTo(AssemblyOrigin.Plugin));
            Assert.That(AssemblyGraphAnalyzer.OriginOf("Assets/plugins/Lower/Lower.asmdef"), Is.EqualTo(AssemblyOrigin.Plugin));
            Assert.That(AssemblyGraphAnalyzer.OriginOf("Assets/Game/Game.Runtime.asmdef"), Is.EqualTo(AssemblyOrigin.Project));
            Assert.That(AssemblyGraphAnalyzer.OriginOf(""), Is.EqualTo(AssemblyOrigin.Project), "the predefined assemblies have no file");
        }
    }
}
