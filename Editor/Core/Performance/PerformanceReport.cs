using System;
using System.Collections.Generic;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// Turns the project's settings and the open scenes' cameras and lights into the Performance report's table of
    /// settings with a cost: render scale, shadow distance and softness, how additional lights are lit, post
    /// processing that runs twice in a camera stack, a depth texture forced on, frame pacing, the graphics API
    /// order, the stack trace every Debug.Log captures in a player, and physics catch-up after a hitch. Mobile
    /// targets get the mobile thresholds. Each row says what the setting does and what to try, not that it is
    /// wrong: some costs buy a look the game wants. Pure: the Editor side only supplies the description.
    /// </summary>
    internal static class PerformanceReport
    {
        public const string Scope = "Project settings and open scenes";

        public const string RenderScaleCheck = "render.scale-native";
        public const string ShadowDistanceCheck = "render.shadow-distance";
        public const string SoftShadowsCheck = "render.soft-shadows";
        public const string AdditionalLightsCheck = "render.additional-lights-per-pixel";
        public const string HdrCheck = "render.hdr-mobile";
        public const string MsaaCheck = "render.msaa-mobile";
        public const string PostTwiceCheck = "camera.post-processing-twice";
        public const string DepthForcedCheck = "camera.depth-texture-forced";
        public const string DirectionalLightsCheck = "light.realtime-directionals";
        public const string FramePacingCheck = "player.frame-pacing-off";
        public const string GraphicsApiOrderCheck = "player.graphics-api-order";
        public const string StackTraceCheck = "player.log-stack-traces";
        public const string MaxTimestepCheck = "time.maximum-allowed-timestep";
        public const string AutoSyncCheck = "physics.auto-sync-transforms";
        public const string AsyncUploadCheck = "quality.async-upload-persistent";

        private const double MobileShadowDistance = 30;
        private const double CatchUpLimit = 0.1;

        public static Report Build(PerformanceSettingsDescription settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            var report = new Report(Area.Performance, Scope);
            int checkedSettings = 0;
            bool mobile = settings.IsMobile;

            if (settings.RenderScale.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.RenderScale.Value >= 1.0)
                {
                    report.Findings.Add(Setting(RenderScaleCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        "Render scale " + settings.RenderScale.Value.ToString("0.##", CultureInfo.InvariantCulture) + ": the world renders at the phone's full resolution, every pixel shaded",
                        "Try 0.8: about a third fewer pixels; overlay cameras in the stack share the scale, so check UI text sharpness", new Measurement(settings.RenderScale.Value * 100, Units.Percent), new Measurement(80, Units.Percent)));
                }
            }

            if (settings.ShadowDistance.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.ShadowDistance.Value > MobileShadowDistance)
                {
                    report.Findings.Add(Setting(ShadowDistanceCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        "Main light shadows reach " + Metres(settings.ShadowDistance.Value) + ": every caster inside it is drawn again into the shadow map",
                        "Bring it in to what the camera shows (often 20 to 25 m on a top-down camera)", new Measurement(settings.ShadowDistance.Value, "m"), new Measurement(25, "m")));
                }
            }

            if (settings.SoftShadows.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.SoftShadows.Value)
                {
                    report.Findings.Add(Setting(SoftShadowsCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        "Soft shadows on a mobile target: every shadowed pixel takes several shadow-map samples",
                        "Turn soft shadows off on the mobile pipeline asset, or keep them only on a high quality level"));
                }
            }

            int maxDirectionals = 0;
            foreach (SceneLightingDescription scene in settings.Scenes)
            {
                maxDirectionals = Math.Max(maxDirectionals, scene.RealtimeDirectionalLights.Count);
            }

            if (settings.AdditionalLights.Length > 0)
            {
                checkedSettings++;
                if (mobile && settings.AdditionalLights == "PerPixel" && maxDirectionals > 1)
                {
                    report.Findings.Add(Setting(AdditionalLightsCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        "Additional lights are lit per pixel and a scene keeps " + maxDirectionals.ToString(CultureInfo.InvariantCulture) + " realtime directional lights: every directional after the first is a per-pixel additional light over the whole screen",
                        "Light additional lights per vertex (on flat-shaded low-poly art it looks the same for a directional light), or fold the extra directional into the ambient"));
                }
            }

            if (settings.Hdr.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.Hdr.Value)
                {
                    report.Findings.Add(Setting(HdrCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        "HDR on a mobile target: the colour buffer doubles in size and bandwidth",
                        "Turn HDR off unless bloom or tone mapping needs the range"));
                }
            }

            if (settings.MsaaSamples.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.MsaaSamples.Value > 2)
                {
                    report.Findings.Add(Setting(MsaaCheck, PipelineSubject(settings), Budget.FrameTime, Severity.Advice,
                        settings.MsaaSamples.Value.ToString(CultureInfo.InvariantCulture) + "x MSAA on a mobile target",
                        "Use 2x, or FXAA on the camera"));
                }
            }

            foreach (CameraDescription camera in settings.Cameras)
            {
                checkedSettings++;
                if (!camera.IsActive)
                {
                    continue;
                }

                if (camera.IsOverlay && camera.PostProcessing)
                {
                    var finding = Setting(PostTwiceCheck, camera.Subject, Budget.FrameTime, Severity.Warning,
                        "An overlay camera with post-processing on: the pipeline applies it to everything rendered before it, so the stack grades and anti-aliases the world twice",
                        "Turn post-processing off on the overlay camera and leave it on the base camera");
                    report.Findings.Add(finding);
                }

                if (camera.DepthTextureForced)
                {
                    var finding = Setting(DepthForcedCheck, camera.Subject, Budget.FrameTime, Severity.Info,
                        "The camera forces the depth texture on: an extra depth copy every frame whatever the pipeline asset says",
                        "");
                    finding.Verdict = "Keep it only if a shader reads the scene depth (soft particles, depth fade, water); otherwise follow the pipeline setting";
                    report.Findings.Add(finding);
                }
            }

            foreach (SceneLightingDescription scene in settings.Scenes)
            {
                if (scene.RealtimeDirectionalLights.Count > 1)
                {
                    var finding = Setting(DirectionalLightsCheck, scene.ScenePath.Length > 0 ? scene.ScenePath : "open scene", Budget.FrameTime, Severity.Advice,
                        scene.RealtimeDirectionalLights.Count.ToString(CultureInfo.InvariantCulture) + " realtime directional lights are active: " + string.Join(", ", scene.RealtimeDirectionalLights),
                        "Keep one realtime directional; bake or fold the others into the ambient, or light additional lights per vertex");
                    report.Findings.Add(finding);
                }
            }

            if (settings.FramePacing.HasValue)
            {
                checkedSettings++;
                if (!settings.FramePacing.Value)
                {
                    report.Findings.Add(Setting(FramePacingCheck, "ProjectSettings/ProjectSettings.asset", Budget.FrameTime, Severity.Advice,
                        "Optimized Frame Pacing is off: frames reach the display unevenly, which reads as stutter at a steady frame rate",
                        "Turn on Player Settings > Android > Optimized Frame Pacing"));
                }
            }

            if (settings.AutoGraphicsApis.HasValue && settings.GraphicsApis.Count > 1)
            {
                checkedSettings++;
                int gles = settings.GraphicsApis.FindIndex(api => api.StartsWith("OpenGLES", StringComparison.Ordinal));
                int vulkan = settings.GraphicsApis.IndexOf("Vulkan");
                if (!settings.AutoGraphicsApis.Value && gles >= 0 && vulkan > gles)
                {
                    report.Findings.Add(Setting(GraphicsApiOrderCheck, "ProjectSettings/ProjectSettings.asset", Budget.FrameTime, Severity.Info,
                        "OpenGL ES is listed before Vulkan: every device that has both runs OpenGL ES, which costs more CPU per draw call",
                        "Put Vulkan first and keep OpenGL ES as the fallback; add the GPUs with known Vulkan driver trouble to the Vulkan deny list"));
                }
            }

            if (settings.LogStackTrace.Length > 0 || settings.WarningStackTrace.Length > 0)
            {
                checkedSettings++;
                bool log = settings.LogStackTrace.Length > 0 && settings.LogStackTrace != "None";
                bool warning = settings.WarningStackTrace.Length > 0 && settings.WarningStackTrace != "None";
                if (log || warning)
                {
                    report.Findings.Add(Setting(StackTraceCheck, "ProjectSettings/ProjectSettings.asset", Budget.FrameTime, Severity.Advice,
                        "Player builds capture a managed stack trace for every " + (log && warning ? "Debug.Log and LogWarning" : log ? "Debug.Log" : "LogWarning") + " (" + (log ? settings.LogStackTrace : settings.WarningStackTrace) + "): a walk of the stack and a string per call",
                        "Set the Log and Warning stack traces to None in Player Settings > Other Settings; keep errors and exceptions at ScriptOnly"));
                }
            }

            if (settings.MaximumAllowedTimestep.HasValue)
            {
                checkedSettings++;
                if (settings.MaximumAllowedTimestep.Value > CatchUpLimit + 1e-6)
                {
                    int steps = settings.FixedTimestep.HasValue && settings.FixedTimestep.Value > 0 ? (int)Math.Round(settings.MaximumAllowedTimestep.Value / settings.FixedTimestep.Value) : 0;
                    report.Findings.Add(Setting(MaxTimestepCheck, "ProjectSettings/TimeManager.asset", Budget.FrameTime, Severity.Advice,
                        "Maximum Allowed Timestep " + settings.MaximumAllowedTimestep.Value.ToString("0.###", CultureInfo.InvariantCulture) + " s" + (steps > 1 ? ": after a hitch a frame may run up to " + steps.ToString(CultureInfo.InvariantCulture) + " physics steps, which makes the next frame late too" : ""),
                        "Lower it to 0.1 s so a slow frame cannot snowball", new Measurement(settings.MaximumAllowedTimestep.Value, Units.Seconds), new Measurement(CatchUpLimit, Units.Seconds)));
                }
            }

            if (settings.AutoSyncTransforms.HasValue)
            {
                checkedSettings++;
                if (settings.AutoSyncTransforms.Value)
                {
                    report.Findings.Add(Setting(AutoSyncCheck, "ProjectSettings/DynamicsManager.asset", Budget.FrameTime, Severity.Advice,
                        "Auto Sync Transforms is on: every transform change is pushed to physics before each query",
                        "Turn it off and call Physics.SyncTransforms where a query must see a transform moved in the same frame"));
                }
            }

            if (settings.AsyncUploadPersistentBuffer.HasValue)
            {
                checkedSettings++;
                if (mobile && settings.AsyncUploadPersistentBuffer.Value)
                {
                    long bytes = (long)Math.Max(0, settings.AsyncUploadBufferMegabytes ?? 0) * 1024 * 1024;
                    var finding = Setting(AsyncUploadCheck, "ProjectSettings/QualitySettings.asset", Budget.RuntimeRam, Severity.Advice,
                        "The " + (settings.QualityLevel.Length > 0 ? settings.QualityLevel + " quality level" : "quality level") + " keeps the async upload buffer for the whole session",
                        "Turn Async Upload Persistent Buffer off: the buffer is then freed when no upload is running");
                    if (bytes > 0)
                    {
                        finding.Measured = Measurement.Bytes(bytes);
                        finding.Projected = Measurement.Bytes(0);
                    }

                    report.Findings.Add(finding);
                }
            }

            report.Metrics.Add(new Metric("settings.checked", Measurement.Count(checkedSettings), Budget.None, "Settings and cameras read"));
            report.Metrics.Add(new Metric("cameras.count", Measurement.Count(settings.Cameras.Count), Budget.None, "Cameras in the open scenes"));
            report.Metrics.Add(new Metric("lights.realtime-directional-max", Measurement.Count(maxDirectionals), Budget.None, "Most realtime directional lights in one open scene"));
            report.Metrics.Add(new Metric("settings.flagged", Measurement.Count(report.Findings.Count), Budget.None, "Settings with a cost"));

            report.Notes.Add("Settings are read for " + (settings.Platform.Length > 0 ? settings.Platform : "the active build target") + (mobile ? ", a mobile target, so the mobile thresholds apply" : "") + ". Cameras and lights come from the scenes open in the Editor; open the scene to check before scanning.");
            report.Notes.Add("A setting with a cost may buy a look the game wants; each row names what to try and what to check after.");
            return report;
        }

        private static Finding Setting(string check, string subject, Budget budget, Severity severity, string title, string verdict, Measurement? measured = null, Measurement? projected = null)
        {
            var finding = new Finding(check, subject)
            {
                Title = title,
                Severity = severity,
                Budget = budget,
                Verdict = verdict ?? "",
                Measured = measured,
                Projected = projected,
            };
            if (subject.IndexOf('/') > 0 && (subject.EndsWith(".asset", StringComparison.Ordinal) || subject.EndsWith(".unity", StringComparison.Ordinal)))
            {
                finding.Evidence.Add(new Evidence(subject, 0, 0, "settings"));
            }

            return finding;
        }

        /// <summary>The pipeline asset path, or a stand-in when the description has none.</summary>
        private static string PipelineSubject(PerformanceSettingsDescription settings)
        {
            return settings.PipelineAsset.Length > 0 ? settings.PipelineAsset : "Render pipeline";
        }

        private static string Metres(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture) + " m";
        }
    }
}
