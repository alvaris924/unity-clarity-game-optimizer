using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ClarityGameOptimizer.Settings
{
    /// <summary>The Project Settings page: third-party folders one per line, the two Architecture thresholds, and a reset.</summary>
    internal static class ClarityGameOptimizerSettingsProvider
    {
        public const string Path = "Project/Clarity Game Optimizer";

        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider(Path, SettingsScope.Project)
            {
                label = "Clarity Game Optimizer",
                keywords = new HashSet<string>(new[] { "optimizer", "architecture", "assembly", "plugins", "third-party", "clarity" }),
                activateHandler = (search, root) => Build(root),
            };
        }

        /// <summary>Builds the page into any root, so it can be exercised without the Settings window.</summary>
        public static void Build(VisualElement root)
        {
            ClarityGameOptimizerSettings settings = ClarityGameOptimizerSettings.instance;
            root.style.paddingLeft = 12;
            root.style.paddingRight = 12;
            root.style.paddingTop = 8;

            var title = new Label("Clarity Game Optimizer") { name = "title" };
            title.style.fontSize = 19;
            title.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            title.style.marginBottom = 8;
            root.Add(title);

            var about = new Label("Settings the team shares. They live in " + ClarityGameOptimizerSettings.AssetPath + "; commit it with the project.") { name = "about" };
            about.style.whiteSpace = WhiteSpace.Normal;
            about.style.marginBottom = 12;
            root.Add(about);

            var foldersHeading = new Label("Third-party folders") { name = "folders-heading" };
            foldersHeading.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            root.Add(foldersHeading);
            var foldersHelp = new Label("One project-relative folder per line. Assemblies under these folders are plugins: drawn, never judged, and never counted as the project's own code.") { name = "folders-help" };
            foldersHelp.style.whiteSpace = WhiteSpace.Normal;
            foldersHelp.style.marginBottom = 4;
            root.Add(foldersHelp);
            var folders = new TextField { name = "third-party-folders", multiline = true, value = string.Join("\n", settings.ThirdPartyFolders) };
            folders.style.minHeight = 72;
            folders.style.marginBottom = 12;
            folders.RegisterValueChangedCallback(change =>
            {
                settings.SetThirdPartyFolders(FolderRules.ParseLines(change.newValue));
                settings.SaveNow();
            });
            root.Add(folders);

            var thresholdsHeading = new Label("Architecture") { name = "architecture-heading" };
            thresholdsHeading.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            root.Add(thresholdsHeading);
            var hub = new IntegerField("Hub fan-in") { name = "hub-fan-in", value = settings.HubFanIn, tooltip = "A project assembly referenced by this many of the project's own assemblies is reported as a hub." };
            hub.RegisterValueChangedCallback(change =>
            {
                settings.SetThresholds(change.newValue, settings.LargeSourceFiles);
                hub.SetValueWithoutNotify(settings.HubFanIn);
                settings.SaveNow();
            });
            root.Add(hub);
            var large = new IntegerField("Large assembly, scripts") { name = "large-source-files", value = settings.LargeSourceFiles, tooltip = "A project assembly with this many scripts or more is reported as large." };
            large.RegisterValueChangedCallback(change =>
            {
                settings.SetThresholds(settings.HubFanIn, change.newValue);
                large.SetValueWithoutNotify(settings.LargeSourceFiles);
                settings.SaveNow();
            });
            root.Add(large);

            var reset = new Button(() =>
            {
                settings.ResetToDefaults();
                settings.SaveNow();
                folders.SetValueWithoutNotify(string.Join("\n", settings.ThirdPartyFolders));
                hub.SetValueWithoutNotify(settings.HubFanIn);
                large.SetValueWithoutNotify(settings.LargeSourceFiles);
            }) { name = "reset", text = "Reset to defaults" };
            reset.style.alignSelf = Align.FlexStart;
            reset.style.marginTop = 12;
            root.Add(reset);
        }
    }
}
