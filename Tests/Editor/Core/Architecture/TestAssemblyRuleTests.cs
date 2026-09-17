using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class TestAssemblyRuleTests
    {
        [Test]
        public void The_include_tests_constraint_marks_a_test_assembly()
        {
            var definition = new AssemblyDefinition(null, new[] { "UNITY_INCLUDE_TESTS" }, null);

            Assert.That(TestAssemblyRule.IsTest(definition), Is.True);
        }

        [Test]
        public void The_legacy_test_assemblies_option_marks_a_test_assembly()
        {
            var definition = new AssemblyDefinition(new[] { "Assembly-CSharp" }, null, new[] { "TestAssemblies" });

            Assert.That(TestAssemblyRule.IsTest(definition), Is.True);
        }

        [TestCase("UnityEngine.TestRunner")]
        [TestCase("UnityEditor.TestRunner")]
        [TestCase("GUID:27619889b8ba8c24980f49ee34dbb44a")]
        [TestCase("GUID:0acc523941302664db1f4e527237feb3")]
        [TestCase("guid:0ACC523941302664DB1F4E527237FEB3")]
        public void An_explicit_test_runner_reference_marks_a_test_assembly(string reference)
        {
            var definition = new AssemblyDefinition(new[] { "Game.Runtime", reference }, null, null);

            Assert.That(TestAssemblyRule.IsTest(definition), Is.True);
        }

        [Test]
        public void An_ordinary_definition_is_not_a_test_assembly()
        {
            var definition = new AssemblyDefinition(new[] { "Game.Runtime", "GUID:1111111111111111aaaaaaaaaaaaaaaa" }, new[] { "UNITY_EDITOR" }, null);

            Assert.That(TestAssemblyRule.IsTest(definition), Is.False);
            Assert.That(TestAssemblyRule.IsTest(AssemblyDefinition.Empty), Is.False);
        }

        [Test]
        public void A_definition_never_holds_null_arrays()
        {
            Assert.That(AssemblyDefinition.Empty.References, Is.Empty);
            Assert.That(AssemblyDefinition.Empty.DefineConstraints, Is.Empty);
            Assert.That(AssemblyDefinition.Empty.OptionalUnityReferences, Is.Empty);
        }
    }
}
