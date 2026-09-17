using System;
using ClarityGameOptimizer.Core;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>Fills in what every report says about where it was made: Unity version, active build target, package version, time.</summary>
    internal static class ReportEnvironment
    {
        public static void Stamp(Report report)
        {
            if (report == null)
            {
                throw new ArgumentNullException(nameof(report));
            }

            report.UnityVersion = Application.unityVersion;
            report.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            report.PackageVersion = PackageVersion();
            report.CreatedAt = DateTime.UtcNow;
        }

        /// <summary>The version from this package's manifest, or empty when the assembly is not running from a package.</summary>
        public static string PackageVersion()
        {
            PackageInfo info = PackageInfo.FindForAssembly(typeof(ReportEnvironment).Assembly);
            return info == null ? "" : info.version;
        }
    }
}
