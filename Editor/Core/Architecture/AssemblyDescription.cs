using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// What the Architecture report needs to know about one script assembly. The Editor side fills these
    /// from the compilation pipeline; tests build them by hand, so the report logic never touches Unity.
    /// </summary>
    internal sealed class AssemblyDescription
    {
        private string _definitionPath = "";

        public AssemblyDescription(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("An assembly needs a name.", nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        /// <summary>Project-relative path of the .asmdef; empty for the predefined Assembly-CSharp family, which has none.</summary>
        public string DefinitionPath
        {
            get { return _definitionPath; }
            set { _definitionPath = value ?? ""; }
        }

        public int SourceFileCount { get; set; }

        /// <summary>Compiled for the Editor only, never for a player.</summary>
        public bool IsEditorOnly { get; set; }

        /// <summary>References the test framework, so it exists to be run by the Test Runner.</summary>
        public bool IsTest { get; set; }

        public AssemblyOrigin Origin { get; set; }

        /// <summary>Names of the script assemblies this one references.</summary>
        public List<string> References { get; } = new List<string>();

        /// <summary>True for the predefined assemblies Unity compiles scripts into when no .asmdef claims them.</summary>
        public bool IsOutsideDefinition
        {
            get { return Origin == AssemblyOrigin.Project && _definitionPath.Length == 0; }
        }
    }
}
