using System;
using System.Collections.Generic;
using System.Linq;

namespace ClarityGameOptimizer.Core
{
    /// <summary>The one order findings are shown in everywhere: most severe first, then the largest, then the subject.</summary>
    internal static class FindingOrder
    {
        public static List<Finding> MostSevereFirst(IEnumerable<Finding> findings)
        {
            if (findings == null)
            {
                throw new ArgumentNullException(nameof(findings));
            }

            return findings
                .OrderByDescending(finding => finding.Severity)
                .ThenByDescending(finding => finding.Measured.HasValue ? finding.Measured.Value.Value : double.NegativeInfinity)
                .ThenBy(finding => finding.Subject, StringComparer.Ordinal)
                .ToList();
        }
    }
}
