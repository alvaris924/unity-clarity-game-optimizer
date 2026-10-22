using System;
using System.Linq;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>
    /// Reads the settings that cost on a device: the Universal Render Pipeline asset in use, the quality level the active
    /// target starts with, time and physics, the player's frame pacing, graphics API order and log stack traces, and the
    /// cameras and realtime directional lights of the open scenes. Pipeline and camera fields are read as serialized
    /// properties, so the package needs no reference to the Universal Render Pipeline. Everything that decides what the
    /// report says lives in <see cref="PerformanceReport"/>.
    /// </summary>
    internal sealed class PerformanceAnalyzer
    {
        public Report Scan()
        {
            Report report = PerformanceReport.Build(Describe());
            ReportEnvironment.Stamp(report);
            return report;
        }

        internal static PerformanceSettingsDescription Describe()
        {
            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            var settings = new PerformanceSettingsDescription
            {
                Platform = target.ToString(),
                IsMobile = target == BuildTarget.Android || target == BuildTarget.iOS,
            };

            DescribePipeline(settings);
            DescribeQuality(settings, BuildPipeline.GetBuildTargetGroup(target).ToString());

            settings.FixedTimestep = Time.fixedDeltaTime;
            settings.MaximumAllowedTimestep = Time.maximumDeltaTime;
            settings.AutoSyncTransforms = Physics.autoSyncTransforms;

            if (target == BuildTarget.Android)
            {
                settings.FramePacing = PlayerSettings.Android.optimizedFramePacing;
            }

            settings.AutoGraphicsApis = PlayerSettings.GetUseDefaultGraphicsAPIs(target);
            settings.GraphicsApis.AddRange(PlayerSettings.GetGraphicsAPIs(target).Select(api => api.ToString()));
            settings.LogStackTrace = PlayerSettings.GetStackTraceLogType(LogType.Log).ToString();
            settings.WarningStackTrace = PlayerSettings.GetStackTraceLogType(LogType.Warning).ToString();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    DescribeScene(settings, scene);
                }
            }

            return settings;
        }

        private static void DescribePipeline(PerformanceSettingsDescription settings)
        {
            RenderPipelineAsset pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null || pipeline.GetType().Name.IndexOf("UniversalRenderPipelineAsset", StringComparison.Ordinal) < 0)
            {
                return;
            }

            settings.PipelineAsset = AssetDatabase.GetAssetPath(pipeline);
            var serialized = new SerializedObject(pipeline);
            settings.RenderScale = ReadDouble(serialized, "m_RenderScale");
            settings.ShadowDistance = ReadDouble(serialized, "m_ShadowDistance");
            settings.SoftShadows = ReadBool(serialized, "m_SoftShadowsSupported");
            settings.AdditionalLightShadows = ReadBool(serialized, "m_AdditionalLightShadowsSupported");
            settings.Hdr = ReadBool(serialized, "m_SupportsHDR");
            settings.MsaaSamples = ReadInt(serialized, "m_MSAA");
            switch (ReadInt(serialized, "m_AdditionalLightsRenderingMode"))
            {
                case 0:
                    settings.AdditionalLights = "Disabled";
                    break;
                case 1:
                    settings.AdditionalLights = "PerPixel";
                    break;
                case 2:
                    settings.AdditionalLights = "PerVertex";
                    break;
            }
        }

        private static void DescribeQuality(PerformanceSettingsDescription settings, string platformGroup)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets.Length == 0)
            {
                return;
            }

            var serialized = new SerializedObject(assets[0]);
            SerializedProperty levels = serialized.FindProperty("m_QualitySettings");
            if (levels == null || !levels.isArray || levels.arraySize == 0)
            {
                return;
            }

            int level = QualitySettings.GetQualityLevel();
            SerializedProperty defaults = serialized.FindProperty("m_PerPlatformDefaultQuality");
            if (defaults != null && defaults.isArray)
            {
                for (int i = 0; i < defaults.arraySize; i++)
                {
                    SerializedProperty entry = defaults.GetArrayElementAtIndex(i);
                    SerializedProperty name = entry.FindPropertyRelative("first");
                    SerializedProperty index = entry.FindPropertyRelative("second");
                    if (name != null && index != null && string.Equals(name.stringValue, platformGroup, StringComparison.OrdinalIgnoreCase))
                    {
                        level = index.intValue;
                    }
                }
            }

            level = Mathf.Clamp(level, 0, levels.arraySize - 1);
            SerializedProperty quality = levels.GetArrayElementAtIndex(level);
            SerializedProperty levelName = quality.FindPropertyRelative("name");
            SerializedProperty persistent = quality.FindPropertyRelative("asyncUploadPersistentBuffer");
            SerializedProperty buffer = quality.FindPropertyRelative("asyncUploadBufferSize");
            settings.QualityLevel = levelName != null ? levelName.stringValue : "";
            settings.AsyncUploadPersistentBuffer = persistent != null ? persistent.boolValue : (bool?)null;
            settings.AsyncUploadBufferMegabytes = buffer != null ? buffer.intValue : (int?)null;
        }

        private static void DescribeScene(PerformanceSettingsDescription settings, Scene scene)
        {
            var lighting = new SceneLightingDescription(scene.path);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    var description = new CameraDescription(scene.path, HierarchyPath(camera.transform))
                    {
                        IsActive = camera.enabled && camera.gameObject.activeInHierarchy,
                    };
                    Component data = camera.GetComponent("UniversalAdditionalCameraData");
                    if (data != null)
                    {
                        var serialized = new SerializedObject(data);
                        description.IsOverlay = ReadInt(serialized, "m_CameraType") == 1;
                        description.PostProcessing = ReadBool(serialized, "m_RenderPostProcessing") == true;
                        description.DepthTextureForced = ReadInt(serialized, "m_RequiresDepthTextureOption") == 1;
                    }

                    settings.Cameras.Add(description);
                }

                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Directional && light.enabled && light.gameObject.activeInHierarchy && light.lightmapBakeType != LightmapBakeType.Baked)
                    {
                        lighting.RealtimeDirectionalLights.Add(HierarchyPath(light.transform));
                    }
                }
            }

            settings.Scenes.Add(lighting);
        }

        private static string HierarchyPath(Transform transform)
        {
            string path = transform.name;
            for (Transform parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        private static double? ReadDouble(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                return null;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return property.floatValue;
                case SerializedPropertyType.Integer:
                    return property.intValue;
                default:
                    return null;
            }
        }

        private static int? ReadInt(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                return null;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    return property.intValue;
                case SerializedPropertyType.Boolean:
                    return property.boolValue ? 1 : 0;
                default:
                    return null;
            }
        }

        private static bool? ReadBool(SerializedObject serialized, string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                return null;
            }

            switch (property.propertyType)
            {
                case SerializedPropertyType.Boolean:
                    return property.boolValue;
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Enum:
                    return property.intValue != 0;
                default:
                    return null;
            }
        }
    }
}
