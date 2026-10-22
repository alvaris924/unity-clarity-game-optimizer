using System;
using System.Globalization;
using UnityEditor;

namespace ClarityGameOptimizer.Analysis
{
    /// <summary>A progress bar updated at most four times a second, so a scan of thousands of assets is not slowed by its own bar.</summary>
    internal sealed class ScanProgress
    {
        private const string Title = "Clarity Game Optimizer";
        private const double Interval = 0.25;
        private double _next;

        public void Report(string what, int index, int count)
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _next)
            {
                return;
            }

            _next = now + Interval;
            EditorUtility.DisplayProgressBar(Title, what + " " + (index + 1).ToString(CultureInfo.InvariantCulture) + " of " + count.ToString(CultureInfo.InvariantCulture), (float)index / Math.Max(1, count));
        }

        public void Clear()
        {
            EditorUtility.ClearProgressBar();
        }
    }
}
