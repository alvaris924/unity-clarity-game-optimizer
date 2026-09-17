namespace ClarityGameOptimizer.Core
{
    /// <summary>Where an assembly's code lives, which decides whether it is the project's own or something it pulled in.</summary>
    internal enum AssemblyOrigin
    {
        /// <summary>Under Assets: the project's own code, including the predefined Assembly-CSharp family.</summary>
        Project = 0,

        /// <summary>Under Assets/Plugins, the folder Unity reserves for third-party code that ships inside the project.</summary>
        Plugin = 1,

        /// <summary>Under Packages: a Unity package, a registry, git or local package, or an embedded one.</summary>
        Package = 2,
    }
}
