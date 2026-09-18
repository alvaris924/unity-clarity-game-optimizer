using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// What the last player build left behind, as plain data: the summary, every file the build wrote or
    /// used, and the per-asset packed sizes when the build recorded them. The Editor side fills it from
    /// the build report; the rules and the report builder never see Unity types.
    /// </summary>
    internal sealed class BuildDescription
    {
        private string _platform = "";
        private string _outputPath = "";
        private string _result = "";
        private string _scriptingBackend = "";
        private string _strippingLevel = "";

        /// <summary>The build target, as Unity names it: Android, iOS, StandaloneWindows64.</summary>
        public string Platform
        {
            get { return _platform; }
            set { _platform = value ?? ""; }
        }

        /// <summary>Where the build went: a file for a package such as an APK, a folder or an executable for a desktop player.</summary>
        public string OutputPath
        {
            get { return _outputPath; }
            set { _outputPath = value ?? ""; }
        }

        /// <summary>Succeeded, Failed, Cancelled or Unknown.</summary>
        public string Result
        {
            get { return _result; }
            set { _result = value ?? ""; }
        }

        /// <summary>When the report file was written; the summary's own end time is not dependable.</summary>
        public DateTime BuiltAt { get; set; }

        /// <summary>Everything the build touched, intermediates included; not a download size.</summary>
        public long TotalBytes { get; set; }

        /// <summary>Whether the build ran with the detailed report option that records per-asset sizes.</summary>
        public bool DetailedRequested { get; set; }

        /// <summary>IL2CPP or Mono, for the built platform as the project is set now.</summary>
        public string ScriptingBackend
        {
            get { return _scriptingBackend; }
            set { _scriptingBackend = value ?? ""; }
        }

        /// <summary>Disabled, Minimal, Low, Medium or High, for the built platform as the project is set now.</summary>
        public string StrippingLevel
        {
            get { return _strippingLevel; }
            set { _strippingLevel = value ?? ""; }
        }

        public List<BuildFileDescription> Files { get; } = new List<BuildFileDescription>();

        public List<PackedAssetDescription> PackedAssets { get; } = new List<PackedAssetDescription>();

        /// <summary>True when the build recorded per-asset sizes; Unity 6 records them only on a detailed build.</summary>
        public bool HasPackedAssets
        {
            get { return PackedAssets.Count > 0; }
        }
    }
}
