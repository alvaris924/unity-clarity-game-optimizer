using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// The settings that cost frame time or memory on a device, read from the project and the open scenes. A null
    /// value means the project did not say (another render pipeline, a module not in use), and the check that reads
    /// it stays silent.
    /// </summary>
    internal sealed class PerformanceSettingsDescription
    {
        private string _platform = "";
        private string _pipelineAsset = "";
        private string _additionalLights = "";
        private string _qualityLevel = "";
        private string _logStackTrace = "";
        private string _warningStackTrace = "";

        /// <summary>The active build target, such as Android.</summary>
        public string Platform
        {
            get { return _platform; }
            set { _platform = value ?? ""; }
        }

        public bool IsMobile { get; set; }

        /// <summary>The path of the Universal Render Pipeline asset in use; empty for other pipelines.</summary>
        public string PipelineAsset
        {
            get { return _pipelineAsset; }
            set { _pipelineAsset = value ?? ""; }
        }

        public double? RenderScale { get; set; }

        public double? ShadowDistance { get; set; }

        public bool? SoftShadows { get; set; }

        /// <summary>PerPixel, PerVertex or Disabled.</summary>
        public string AdditionalLights
        {
            get { return _additionalLights; }
            set { _additionalLights = value ?? ""; }
        }

        public bool? AdditionalLightShadows { get; set; }

        public bool? Hdr { get; set; }

        public int? MsaaSamples { get; set; }

        /// <summary>The quality level the active target starts with.</summary>
        public string QualityLevel
        {
            get { return _qualityLevel; }
            set { _qualityLevel = value ?? ""; }
        }

        public bool? AsyncUploadPersistentBuffer { get; set; }

        public int? AsyncUploadBufferMegabytes { get; set; }

        public double? FixedTimestep { get; set; }

        public double? MaximumAllowedTimestep { get; set; }

        public bool? AutoSyncTransforms { get; set; }

        /// <summary>Android's Optimized Frame Pacing; null on other targets.</summary>
        public bool? FramePacing { get; set; }

        /// <summary>Whether the Editor picks the graphics APIs; false when the list below is the project's own order.</summary>
        public bool? AutoGraphicsApis { get; set; }

        public List<string> GraphicsApis { get; } = new List<string>();

        /// <summary>The managed stack trace a Debug.Log captures in a player build: None, ScriptOnly or Full.</summary>
        public string LogStackTrace
        {
            get { return _logStackTrace; }
            set { _logStackTrace = value ?? ""; }
        }

        public string WarningStackTrace
        {
            get { return _warningStackTrace; }
            set { _warningStackTrace = value ?? ""; }
        }

        public List<CameraDescription> Cameras { get; } = new List<CameraDescription>();

        public List<SceneLightingDescription> Scenes { get; } = new List<SceneLightingDescription>();
    }

    /// <summary>A camera in an open scene, with the Universal Render Pipeline's per-camera settings.</summary>
    internal sealed class CameraDescription
    {
        public CameraDescription(string scenePath, string objectPath)
        {
            if (string.IsNullOrWhiteSpace(objectPath))
            {
                throw new ArgumentException("A camera needs its hierarchy path.", nameof(objectPath));
            }

            ScenePath = scenePath ?? "";
            ObjectPath = objectPath;
        }

        public string ScenePath { get; }

        /// <summary>The hierarchy path, such as "Main Camera" or "UI/UI Camera".</summary>
        public string ObjectPath { get; }

        public bool IsActive { get; set; }

        /// <summary>An overlay camera renders on top of a base camera's target.</summary>
        public bool IsOverlay { get; set; }

        public bool PostProcessing { get; set; }

        /// <summary>The camera asks for the depth texture whatever the pipeline asset says.</summary>
        public bool DepthTextureForced { get; set; }

        public string Subject
        {
            get { return ScenePath.Length == 0 ? ObjectPath : ScenePath + "/" + ObjectPath; }
        }
    }

    /// <summary>The realtime directional lights an open scene keeps active.</summary>
    internal sealed class SceneLightingDescription
    {
        public SceneLightingDescription(string scenePath)
        {
            ScenePath = scenePath ?? "";
        }

        public string ScenePath { get; }

        /// <summary>Hierarchy paths of the enabled, non-baked directional lights.</summary>
        public List<string> RealtimeDirectionalLights { get; } = new List<string>();
    }
}
