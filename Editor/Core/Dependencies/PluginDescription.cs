using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A third-party plugin that ships inside the project as a folder of sources and libraries.</summary>
    internal sealed class PluginDescription
    {
        public PluginDescription(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new ArgumentException("A plugin needs a folder.", nameof(folder));
            }

            Folder = folder.Replace('\\', '/').TrimEnd('/');
        }

        /// <summary>Project-relative, forward slashes, no trailing slash.</summary>
        public string Folder { get; }

        public string Name
        {
            get
            {
                int slash = Folder.LastIndexOf('/');
                return slash < 0 ? Folder : Folder.Substring(slash + 1);
            }
        }

        public int DefinitionCount { get; set; }

        public int LibraryCount { get; set; }

        public int ScriptCount { get; set; }

        /// <summary>Scripts no assembly definition claims, which compile into the predefined assemblies with the project's own code.</summary>
        public int ScriptsOutsideDefinitions { get; set; }

        /// <summary>Size of the folder on disk, or -1 when unknown.</summary>
        public long Bytes { get; set; } = -1;
    }
}
