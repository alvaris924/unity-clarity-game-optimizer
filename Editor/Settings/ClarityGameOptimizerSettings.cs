using System;
using System.Collections.Generic;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;

namespace ClarityGameOptimizer.Settings
{
    /// <summary>
    /// The settings a team shares through version control, stored at ProjectSettings/ClarityGameOptimizer.asset:
    /// which folders hold third-party code, and the thresholds the Architecture checks use. Per-user
    /// choices such as where archify lives are EditorPrefs, not here.
    /// </summary>
    [FilePath(AssetPath, FilePathAttribute.Location.ProjectFolder)]
    internal sealed class ClarityGameOptimizerSettings : ScriptableSingleton<ClarityGameOptimizerSettings>
    {
        public const string AssetPath = "ProjectSettings/ClarityGameOptimizer.asset";

        public static readonly string[] DefaultThirdPartyFolders = { "Assets/Plugins" };

        [SerializeField]
        private List<string> _thirdPartyFolders = new List<string>(DefaultThirdPartyFolders);

        [SerializeField]
        private int _hubFanIn = ArchitectureRules.DefaultHubFanIn;

        [SerializeField]
        private int _largeSourceFiles = ArchitectureRules.DefaultLargeSourceFiles;

        /// <summary>Project-relative folders whose assemblies are plugins rather than the project's own code, cleaned and without duplicates.</summary>
        public IReadOnlyList<string> ThirdPartyFolders
        {
            get { return FolderRules.Normalize(_thirdPartyFolders); }
        }

        public int HubFanIn
        {
            get { return Math.Max(1, _hubFanIn); }
        }

        public int LargeSourceFiles
        {
            get { return Math.Max(1, _largeSourceFiles); }
        }

        public ArchitectureRules ArchitectureRules
        {
            get { return new ArchitectureRules(HubFanIn, LargeSourceFiles); }
        }

        public void SetThirdPartyFolders(IEnumerable<string> folders)
        {
            _thirdPartyFolders = FolderRules.Normalize(folders);
        }

        public void SetThresholds(int hubFanIn, int largeSourceFiles)
        {
            _hubFanIn = Math.Max(1, hubFanIn);
            _largeSourceFiles = Math.Max(1, largeSourceFiles);
        }

        public void ResetToDefaults()
        {
            _thirdPartyFolders = new List<string>(DefaultThirdPartyFolders);
            _hubFanIn = ArchitectureRules.DefaultHubFanIn;
            _largeSourceFiles = ArchitectureRules.DefaultLargeSourceFiles;
        }

        /// <summary>Writes the asset as text so it diffs in version control.</summary>
        public void SaveNow()
        {
            Save(true);
        }
    }
}
