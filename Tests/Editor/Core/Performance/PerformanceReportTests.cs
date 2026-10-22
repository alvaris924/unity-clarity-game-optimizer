using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class PerformanceReportTests
    {
        private static Report Build(PerformanceSettingsDescription settings)
        {
            Report report = PerformanceReport.Build(settings);
            report.CreatedAt = new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc);
            Assert.That(ReportValidator.Validate(report), Is.Empty);
            return report;
        }

        private static PerformanceSettingsDescription Mobile()
        {
            return new PerformanceSettingsDescription { Platform = "Android", IsMobile = true, PipelineAsset = "Assets/Settings/URP.asset" };
        }

        private static string[] Checks(Report report)
        {
            return report.Findings.Select(f => f.Check).ToArray();
        }

        [Test]
        public void Mobile_pipeline_costs_are_flagged_and_desktop_ones_are_not()
        {
            PerformanceSettingsDescription mobile = Mobile();
            mobile.RenderScale = 1.0;
            mobile.ShadowDistance = 40;
            mobile.SoftShadows = true;
            mobile.Hdr = true;
            mobile.MsaaSamples = 4;
            Report mobileReport = Build(mobile);
            Assert.That(Checks(mobileReport), Is.SupersetOf(new[]
            {
                PerformanceReport.RenderScaleCheck, PerformanceReport.ShadowDistanceCheck, PerformanceReport.SoftShadowsCheck, PerformanceReport.HdrCheck, PerformanceReport.MsaaCheck,
            }));
            Finding scale = mobileReport.Findings.Single(f => f.Check == PerformanceReport.RenderScaleCheck);
            Assert.That(scale.Budget, Is.EqualTo(Budget.FrameTime));
            Assert.That(scale.Measured.Value.Value, Is.EqualTo(100));

            var desktop = new PerformanceSettingsDescription { Platform = "StandaloneWindows64", RenderScale = 1.0, ShadowDistance = 150, SoftShadows = true, Hdr = true, MsaaSamples = 8 };
            Assert.That(Build(desktop).Findings, Is.Empty);
        }

        [Test]
        public void Tuned_mobile_settings_pass()
        {
            PerformanceSettingsDescription settings = Mobile();
            settings.RenderScale = 0.8;
            settings.ShadowDistance = 25;
            settings.SoftShadows = false;
            settings.AdditionalLights = "PerVertex";
            settings.FramePacing = true;
            settings.LogStackTrace = "None";
            settings.WarningStackTrace = "None";
            settings.FixedTimestep = 1.0 / 60;
            settings.MaximumAllowedTimestep = 0.1;
            settings.AutoSyncTransforms = false;
            settings.AsyncUploadPersistentBuffer = false;
            Assert.That(Build(settings).Findings, Is.Empty);
        }

        [Test]
        public void An_overlay_camera_with_post_processing_runs_it_twice()
        {
            PerformanceSettingsDescription settings = Mobile();
            settings.Cameras.Add(new CameraDescription("Assets/Game.unity", "Main Camera") { IsActive = true, PostProcessing = true, DepthTextureForced = true });
            settings.Cameras.Add(new CameraDescription("Assets/Game.unity", "UI/UI Camera") { IsActive = true, IsOverlay = true, PostProcessing = true });
            settings.Cameras.Add(new CameraDescription("Assets/Game.unity", "Off Camera") { IsActive = false, IsOverlay = true, PostProcessing = true });
            Report report = Build(settings);

            Finding twice = report.Findings.Single(f => f.Check == PerformanceReport.PostTwiceCheck);
            Assert.That(twice.Subject, Is.EqualTo("Assets/Game.unity/UI/UI Camera"));
            Assert.That(twice.Severity, Is.EqualTo(Severity.Warning));
            Assert.That(report.Findings.Single(f => f.Check == PerformanceReport.DepthForcedCheck).Severity, Is.EqualTo(Severity.Info));
        }

        [Test]
        public void Several_realtime_directionals_with_per_pixel_additional_lights_are_flagged()
        {
            PerformanceSettingsDescription settings = Mobile();
            settings.AdditionalLights = "PerPixel";
            var scene = new SceneLightingDescription("Assets/Game.unity");
            scene.RealtimeDirectionalLights.Add("Sun");
            scene.RealtimeDirectionalLights.Add("Fill");
            settings.Scenes.Add(scene);
            Report report = Build(settings);

            Assert.That(Checks(report), Is.SupersetOf(new[] { PerformanceReport.AdditionalLightsCheck, PerformanceReport.DirectionalLightsCheck }));
            Assert.That(report.Metrics.Single(m => m.Key == "lights.realtime-directional-max").Value.Value, Is.EqualTo(2));
        }

        [Test]
        public void Player_and_time_settings_with_a_cost()
        {
            PerformanceSettingsDescription settings = Mobile();
            settings.FramePacing = false;
            settings.AutoGraphicsApis = false;
            settings.GraphicsApis.AddRange(new[] { "OpenGLES3", "Vulkan" });
            settings.LogStackTrace = "ScriptOnly";
            settings.WarningStackTrace = "None";
            settings.FixedTimestep = 1.0 / 60;
            settings.MaximumAllowedTimestep = 0.3333333;
            settings.AutoSyncTransforms = true;
            settings.AsyncUploadPersistentBuffer = true;
            settings.AsyncUploadBufferMegabytes = 16;
            settings.QualityLevel = "Mobile";
            Report report = Build(settings);

            Assert.That(Checks(report), Is.SupersetOf(new[]
            {
                PerformanceReport.FramePacingCheck, PerformanceReport.GraphicsApiOrderCheck, PerformanceReport.StackTraceCheck,
                PerformanceReport.MaxTimestepCheck, PerformanceReport.AutoSyncCheck, PerformanceReport.AsyncUploadCheck,
            }));
            Assert.That(report.Findings.Single(f => f.Check == PerformanceReport.MaxTimestepCheck).Title, Does.Contain("up to 20 physics steps"));
            Assert.That(report.Findings.Single(f => f.Check == PerformanceReport.StackTraceCheck).Title, Does.Contain("Debug.Log (ScriptOnly)"));
            Finding upload = report.Findings.Single(f => f.Check == PerformanceReport.AsyncUploadCheck);
            Assert.That(upload.Budget, Is.EqualTo(Budget.RuntimeRam));
            Assert.That(upload.Measured.Value.Value, Is.EqualTo(16L * 1024 * 1024));
        }

        [Test]
        public void Vulkan_first_or_automatic_apis_pass()
        {
            PerformanceSettingsDescription manual = Mobile();
            manual.AutoGraphicsApis = false;
            manual.GraphicsApis.AddRange(new[] { "Vulkan", "OpenGLES3" });
            Assert.That(Checks(Build(manual)), Has.No.Member(PerformanceReport.GraphicsApiOrderCheck));

            PerformanceSettingsDescription automatic = Mobile();
            automatic.AutoGraphicsApis = true;
            automatic.GraphicsApis.AddRange(new[] { "OpenGLES3", "Vulkan" });
            Assert.That(Checks(Build(automatic)), Has.No.Member(PerformanceReport.GraphicsApiOrderCheck));
        }

        [Test]
        public void A_description_without_a_pipeline_asset_still_names_a_subject()
        {
            var settings = new PerformanceSettingsDescription { Platform = "iOS", IsMobile = true, RenderScale = 1.0 };
            Assert.That(Build(settings).Findings.Single().Subject, Is.EqualTo("Render pipeline"));
        }
    }
}
