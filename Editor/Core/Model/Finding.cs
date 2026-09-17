using System;
using System.Collections.Generic;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// One row of a report: a check that fired on a subject, with how much it costs, what to do about it,
    /// and where to look. The id is the check and the subject joined, so a report holds at most one finding
    /// per check per subject and two reports can be diffed row by row.
    /// </summary>
    internal sealed class Finding
    {
        private string _title = "";
        private string _verdict = "";

        public Finding(string check, string subject)
        {
            if (string.IsNullOrWhiteSpace(check))
            {
                throw new ArgumentException("A finding needs the check that produced it.", nameof(check));
            }

            if (string.IsNullOrWhiteSpace(subject))
            {
                throw new ArgumentException("A finding needs a subject.", nameof(subject));
            }

            Check = check;
            Subject = subject;
            Id = check + ":" + subject;
        }

        /// <summary>"check:subject", unique within a report.</summary>
        public string Id { get; }

        /// <summary>The rule that produced the finding, such as "texture.stored-uncompressed".</summary>
        public string Check { get; }

        /// <summary>What the finding is about: an asset path, an assembly name, a package id. Cross-links between areas key on it.</summary>
        public string Subject { get; }

        /// <summary>What is wrong, in one line.</summary>
        public string Title
        {
            get { return _title; }
            set { _title = value ?? ""; }
        }

        public Severity Severity { get; set; }

        /// <summary>The budget the measured and projected values belong to.</summary>
        public Budget Budget { get; set; }

        /// <summary>What it costs today; null when the finding carries no number.</summary>
        public Measurement? Measured { get; set; }

        /// <summary>What it would cost after the fix; null when there is no fix or no estimate.</summary>
        public Measurement? Projected { get; set; }

        /// <summary>What to do, in one line. Required above <see cref="Severity.Info"/>.</summary>
        public string Verdict
        {
            get { return _verdict; }
            set { _verdict = value ?? ""; }
        }

        /// <summary>The fix that applies, or null when the finding is informational or needs a person.</summary>
        public string FixId { get; set; }

        public List<Evidence> Evidence { get; } = new List<Evidence>();
    }
}
