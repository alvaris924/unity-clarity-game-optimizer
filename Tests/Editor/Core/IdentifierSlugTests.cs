using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class IdentifierSlugTests
    {
        [TestCase("Game.Runtime", "Game-Runtime")]
        [TestCase("Assembly-CSharp", "Assembly-CSharp")]
        [TestCase("Assembly-CSharp-Editor", "Assembly-CSharp-Editor")]
        [TestCase("PlayFab CBS wrappers", "PlayFab-CBS-wrappers")]
        [TestCase("Unity.TextMeshPro", "Unity-TextMeshPro")]
        [TestCase("a...b", "a-b")]
        [TestCase("  padded  ", "padded")]
        [TestCase("snake_case_kept", "snake_case_kept")]
        [TestCase("_leading", "leading")]
        [TestCase("-leading", "leading")]
        [TestCase("trailing_", "trailing")]
        [TestCase("trailing-", "trailing")]
        public void From_keeps_letters_digits_and_separators_and_collapses_the_rest(string name, string expected)
        {
            Assert.That(IdentifierSlug.From(name), Is.EqualTo(expected));
        }

        [TestCase("3D Models", "n3D-Models")]
        [TestCase("_3d", "n3d")]
        [TestCase("", "n")]
        [TestCase("   ", "n")]
        [TestCase(null, "n")]
        [TestCase("日本語", "n")]
        public void From_prefixes_when_the_name_cannot_start_an_identifier(string name, string expected)
        {
            Assert.That(IdentifierSlug.From(name), Is.EqualTo(expected));
        }

        [TestCase("Game.Runtime")]
        [TestCase("Assets/Project Data/Game/Scripts")]
        [TestCase("com.unity.render-pipelines.universal")]
        [TestCase("9 lives")]
        [TestCase("")]
        [TestCase(null)]
        public void From_always_yields_a_valid_identifier(string name)
        {
            Assert.That(IdentifierSlug.IsValid(IdentifierSlug.From(name)), Is.True, name);
        }

        [TestCase("game", true)]
        [TestCase("Game-Runtime_2", true)]
        [TestCase("n3D", true)]
        [TestCase("3D", false)]
        [TestCase("-x", false)]
        [TestCase("a.b", false)]
        [TestCase("a b", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsValid_matches_the_archify_id_rule(string id, bool expected)
        {
            Assert.That(IdentifierSlug.IsValid(id), Is.EqualTo(expected));
        }

        [Test]
        public void Unique_returns_the_slug_itself_when_free_and_reserves_it()
        {
            var taken = new HashSet<string>();

            Assert.That(IdentifierSlug.Unique("game", taken), Is.EqualTo("game"));
            Assert.That(taken, Does.Contain("game"));
        }

        [Test]
        public void Unique_appends_the_first_free_numeric_suffix()
        {
            var taken = new HashSet<string> { "game", "game-2" };

            Assert.That(IdentifierSlug.Unique("game", taken), Is.EqualTo("game-3"));
            Assert.That(taken, Does.Contain("game-3"));
        }

        [Test]
        public void Unique_results_stay_valid_identifiers()
        {
            var taken = new HashSet<string> { "n" };

            Assert.That(IdentifierSlug.IsValid(IdentifierSlug.Unique("n", taken)), Is.True);
        }
    }
}
