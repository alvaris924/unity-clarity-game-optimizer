namespace ClarityGameOptimizer.Core
{
    /// <summary>Where a package was resolved from, in the Package Manager's own terms.</summary>
    internal enum PackageSourceKind
    {
        Unknown = 0,

        /// <summary>A registry such as Unity's or a scoped one; pinned by version.</summary>
        Registry = 1,

        /// <summary>Ships with the Editor; the module packages.</summary>
        BuiltIn = 2,

        /// <summary>Lives inside the project's Packages folder and is versioned with it.</summary>
        Embedded = 3,

        /// <summary>A file: path on this machine.</summary>
        Local = 4,

        /// <summary>A git URL, pinned only when its revision is a commit.</summary>
        Git = 5,

        /// <summary>A local tarball.</summary>
        Tarball = 6,
    }
}
