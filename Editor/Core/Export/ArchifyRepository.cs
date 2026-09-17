using System;
using System.Text.RegularExpressions;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// The public GitHub repository and the exact revision a diagram's source evidence points into. archify
    /// verifies every source against git, so evidence is only written when the project is on public GitHub
    /// and the caller says which commit; a private project gets a diagram without source capsules.
    /// </summary>
    internal sealed class ArchifyRepository
    {
        private static readonly Regex UrlPattern = new Regex("^https://github\\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+(?:\\.git)?/?$", RegexOptions.Compiled);
        private static readonly Regex RevisionPattern = new Regex("^[a-fA-F0-9]{40}$", RegexOptions.Compiled);

        public ArchifyRepository(string url, string revision)
        {
            if (url == null || !UrlPattern.IsMatch(url))
            {
                throw new ArgumentException("The repository must be a public GitHub URL such as https://github.com/owner/name.", nameof(url));
            }

            if (revision == null || !RevisionPattern.IsMatch(revision))
            {
                throw new ArgumentException("The revision must be a full 40-character commit hash.", nameof(revision));
            }

            Url = url;
            Revision = revision.ToLowerInvariant();
        }

        public string Url { get; }

        public string Revision { get; }
    }
}
