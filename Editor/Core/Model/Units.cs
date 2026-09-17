using System;
using System.Globalization;

namespace ClarityGameOptimizer.Core
{
    /// <summary>
    /// The unit strings a measurement carries and how each one is shown. Units are strings rather than an
    /// enum so an analyzer can measure something new without touching the model; the ones named here get
    /// a readable format, anything else prints as "value unit". Every format is culture-invariant, so a
    /// report reads the same on a machine that writes decimals with a comma.
    /// </summary>
    internal static class Units
    {
        public const string Bytes = "bytes";
        public const string Milliseconds = "ms";
        public const string Seconds = "s";
        public const string Count = "count";
        public const string Percent = "percent";

        private const double Kilo = 1024d;

        public static string Format(double value, string unit)
        {
            switch (unit)
            {
                case Bytes:
                    return FormatBytes(value);
                case Milliseconds:
                    return value.ToString("0.#", CultureInfo.InvariantCulture) + " ms";
                case Seconds:
                    return value.ToString("0.##", CultureInfo.InvariantCulture) + " s";
                case Count:
                    return value.ToString("N0", CultureInfo.InvariantCulture);
                case Percent:
                    return value.ToString("0.#", CultureInfo.InvariantCulture) + " %";
                default:
                    string number = value.ToString("R", CultureInfo.InvariantCulture);
                    return string.IsNullOrEmpty(unit) ? number : number + " " + unit;
            }
        }

        /// <summary>1024-based with one decimal at most, the way the Editor and the build report show sizes.</summary>
        public static string FormatBytes(double bytes)
        {
            if (bytes < 0)
            {
                return "-" + FormatBytes(-bytes);
            }

            if (bytes < Kilo)
            {
                return Math.Round(bytes).ToString("0", CultureInfo.InvariantCulture) + " B";
            }

            double kilobytes = bytes / Kilo;
            if (kilobytes < Kilo)
            {
                return kilobytes.ToString("0.#", CultureInfo.InvariantCulture) + " KB";
            }

            double megabytes = kilobytes / Kilo;
            if (megabytes < Kilo)
            {
                return megabytes.ToString("0.#", CultureInfo.InvariantCulture) + " MB";
            }

            return (megabytes / Kilo).ToString("0.##", CultureInfo.InvariantCulture) + " GB";
        }
    }
}
