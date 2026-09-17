using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class FolderRulesTests
    {
        [Test]
        public void Normalize_cleans_and_deduplicates_while_keeping_the_order()
        {
            var folders = FolderRules.Normalize(new[] { " Assets/Plugins/ ", "", "Assets\\Feel", "./Assets/Feel/", "assets/plugins", null, "Assets/ChocDino" });

            Assert.That(folders, Is.EqualTo(new[] { "Assets/Plugins", "Assets/Feel", "Assets/ChocDino" }));
            Assert.That(FolderRules.Normalize(null), Is.Empty);
        }

        [Test]
        public void ParseLines_takes_one_folder_per_line()
        {
            Assert.That(FolderRules.ParseLines("Assets/Plugins\r\n\nAssets/Feel\n"), Is.EqualTo(new[] { "Assets/Plugins", "Assets/Feel" }));
            Assert.That(FolderRules.ParseLines(null), Is.Empty);
        }

        [TestCase("Assets/Plugins/UniTask/UniTask.asmdef", "Assets/Plugins", true)]
        [TestCase("Assets/Plugins", "Assets/Plugins", true)]
        [TestCase("Assets\\plugins\\X.asmdef", "Assets/Plugins/", true)]
        [TestCase("Assets/Plugins2/X.asmdef", "Assets/Plugins", false)]
        [TestCase("Assets/Game/X.asmdef", "Assets/Plugins", false)]
        [TestCase("Assets/Plugins/X.asmdef", "", false)]
        [TestCase("", "Assets/Plugins", false)]
        public void IsUnder_matches_the_folder_and_its_contents_only(string path, string folder, bool expected)
        {
            Assert.That(FolderRules.IsUnder(path, folder), Is.EqualTo(expected));
        }

        [Test]
        public void IsUnderAny_checks_every_folder()
        {
            string[] folders = { "Assets/Plugins", "Assets/Feel" };

            Assert.That(FolderRules.IsUnderAny("Assets/Feel/MMTools/MoreMountains.Tools.asmdef", folders), Is.True);
            Assert.That(FolderRules.IsUnderAny("Assets/Game/Game.asmdef", folders), Is.False);
            Assert.That(FolderRules.IsUnderAny("Assets/Plugins/X.asmdef", null), Is.False);
        }
    }
}
