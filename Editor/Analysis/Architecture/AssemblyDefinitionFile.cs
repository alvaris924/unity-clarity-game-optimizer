using System;
using System.IO;
using ClarityGameOptimizer.Core;
using UnityEngine;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>Reads the fields of an .asmdef file the report cares about, with the Editor's own JSON reader.</summary>
    internal static class AssemblyDefinitionFile
    {
        [Serializable]
        private sealed class Json
        {
            public string[] references;
            public string[] defineConstraints;
            public string[] optionalUnityReferences;
        }

        /// <summary>Reads the file at a project-relative path under Assets; anything else, or an unreadable file, reads as empty.</summary>
        public static AssemblyDefinition Read(string projectRelativePath)
        {
            if (string.IsNullOrEmpty(projectRelativePath) || !projectRelativePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return AssemblyDefinition.Empty;
            }

            string fullPath = Path.Combine(ReportFolders.ProjectRoot, projectRelativePath);
            if (!File.Exists(fullPath))
            {
                return AssemblyDefinition.Empty;
            }

            return Parse(File.ReadAllText(fullPath));
        }

        public static AssemblyDefinition Parse(string text)
        {
            Json json;
            try
            {
                json = JsonUtility.FromJson<Json>(text);
            }
            catch (ArgumentException)
            {
                return AssemblyDefinition.Empty;
            }

            return json == null ? AssemblyDefinition.Empty : new AssemblyDefinition(json.references, json.defineConstraints, json.optionalUnityReferences);
        }
    }
}
