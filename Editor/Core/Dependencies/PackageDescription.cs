using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ClarityGameOptimizer.Core
{
    /// <summary>What the Dependencies report needs to know about one resolved package. The Editor side fills these from the Package Manager.</summary>
    internal sealed class PackageDescription
    {
        private static readonly Regex CommitHash = new Regex("^[a-fA-F0-9]{40}$", RegexOptions.Compiled);
        private static readonly Regex VersionTag = new Regex("^v?\\d+(\\.\\d+)*([-.+][A-Za-z0-9.-]*)?$", RegexOptions.Compiled);

        private string _displayName = "";
        private string _version = "";
        private string _gitRevision = "";
        private string _gitHash = "";

        public PackageDescription(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("A package needs a name.", nameof(name));
            }

            Name = name;
        }

        public string Name { get; }

        public string DisplayName
        {
            get { return _displayName.Length > 0 ? _displayName : Name; }
            set { _displayName = value ?? ""; }
        }

        public string Version
        {
            get { return _version; }
            set { _version = value ?? ""; }
        }

        public PackageSourceKind Source { get; set; }

        /// <summary>Named in the manifest, as opposed to pulled in by another package.</summary>
        public bool IsDirect { get; set; }

        /// <summary>For a git package, the ref the manifest asked for: a commit, a tag, a branch, or empty for the default branch.</summary>
        public string GitRevision
        {
            get { return _gitRevision; }
            set { _gitRevision = value ?? ""; }
        }

        /// <summary>For a git package, the commit that was resolved.</summary>
        public string GitHash
        {
            get { return _gitHash; }
            set { _gitHash = value ?? ""; }
        }

        /// <summary>Size of the resolved folder on disk, or -1 when unknown. A footprint, not a build size.</summary>
        public long Bytes { get; set; } = -1;

        /// <summary>Names of the packages this one depends on.</summary>
        public List<string> Dependencies { get; } = new List<string>();

        /// <summary>How many of the project's own assembly definitions reference an assembly of this package.</summary>
        public int ProjectReferrers { get; set; }

        /// <summary>The package's folder as the asset database sees it.</summary>
        public string AssetPath
        {
            get { return "Packages/" + Name; }
        }

        public bool IsGitPinnedToCommit
        {
            get { return Source == PackageSourceKind.Git && CommitHash.IsMatch(_gitRevision); }
        }

        /// <summary>A git package following a tag that looks like a version, which moves rarely but can.</summary>
        public bool IsGitOnVersionTag
        {
            get { return Source == PackageSourceKind.Git && !IsGitPinnedToCommit && VersionTag.IsMatch(_gitRevision); }
        }

        /// <summary>A git package following a branch or the default branch, which moves whenever someone pushes.</summary>
        public bool IsGitOnBranch
        {
            get { return Source == PackageSourceKind.Git && !IsGitPinnedToCommit && !IsGitOnVersionTag; }
        }

        /// <summary>The resolved commit, shortened, for labels.</summary>
        public string ShortHash
        {
            get { return _gitHash.Length > 7 ? _gitHash.Substring(0, 7) : _gitHash; }
        }
    }
}
