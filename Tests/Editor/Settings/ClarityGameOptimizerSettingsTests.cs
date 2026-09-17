using ClarityGameOptimizer.Settings;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace ClarityGameOptimizer.Tests.Settings
{
    /// <summary>The singleton is changed in memory only; nothing here saves the asset.</summary>
    public class ClarityGameOptimizerSettingsTests
    {
        [TearDown]
        public void RestoreDefaults()
        {
            ClarityGameOptimizerSettings.instance.ResetToDefaults();
        }

        [Test]
        public void Defaults_are_the_plugins_folder_and_the_shipped_thresholds()
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;
            settings.ResetToDefaults();

            Assert.That(settings.ThirdPartyFolders, Is.EqualTo(new[] { "Assets/Plugins" }));
            Assert.That(settings.HubFanIn, Is.EqualTo(5));
            Assert.That(settings.LargeSourceFiles, Is.EqualTo(250));
            Assert.That(settings.ArchitectureRules.HubFanIn, Is.EqualTo(5));
        }

        [Test]
        public void Folders_are_normalised_and_thresholds_never_drop_below_one()
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;

            settings.SetThirdPartyFolders(new[] { " Assets/Feel/ ", "Assets\\ChocDino", "Assets/Feel" });
            settings.SetThresholds(0, -3);

            Assert.That(settings.ThirdPartyFolders, Is.EqualTo(new[] { "Assets/Feel", "Assets/ChocDino" }));
            Assert.That(settings.HubFanIn, Is.EqualTo(1));
            Assert.That(settings.LargeSourceFiles, Is.EqualTo(1));
            Assert.That(settings.ArchitectureRules.LargeSourceFiles, Is.EqualTo(1));
        }

        [Test]
        public void The_settings_page_shows_the_current_values()
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;
            settings.SetThirdPartyFolders(new[] { "Assets/Plugins", "Assets/Feel" });
            settings.SetThresholds(7, 300);
            var root = new VisualElement();

            ClarityGameOptimizerSettingsProvider.Build(root);

            Assert.That(root.Q<TextField>("third-party-folders").value, Is.EqualTo("Assets/Plugins\nAssets/Feel"));
            Assert.That(root.Q<IntegerField>("hub-fan-in").value, Is.EqualTo(7));
            Assert.That(root.Q<IntegerField>("large-source-files").value, Is.EqualTo(300));
            Assert.That(root.Q<Button>("reset"), Is.Not.Null);
            Assert.That(ClarityGameOptimizerSettingsProvider.Create().settingsPath, Is.EqualTo("Project/Clarity Game Optimizer"));
        }
    }
}
