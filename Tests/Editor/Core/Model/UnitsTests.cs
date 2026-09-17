using System.Globalization;
using ClarityGameOptimizer.Core;
using NUnit.Framework;

namespace ClarityGameOptimizer.Tests.Core
{
    public class UnitsTests
    {
        [TestCase(0, "0 B")]
        [TestCase(1023, "1023 B")]
        [TestCase(1024, "1 KB")]
        [TestCase(1536, "1.5 KB")]
        [TestCase(734003, "716.8 KB")]
        [TestCase(12480102, "11.9 MB")]
        [TestCase(233832448, "223 MB")]
        [TestCase(1610612736, "1.5 GB")]
        [TestCase(-1536, "-1.5 KB")]
        public void Formats_bytes_in_1024_steps_with_one_decimal_at_most(double bytes, string expected)
        {
            Assert.That(Units.FormatBytes(bytes), Is.EqualTo(expected));
        }

        [TestCase(12.34, Units.Milliseconds, "12.3 ms")]
        [TestCase(1500, Units.Milliseconds, "1500 ms")]
        [TestCase(2.5, Units.Seconds, "2.5 s")]
        [TestCase(1026, Units.Count, "1,026")]
        [TestCase(41.25, Units.Percent, "41.3 %")]
        [TestCase(3.5, "draw calls", "3.5 draw calls")]
        [TestCase(7, "", "7")]
        public void Formats_the_named_units_and_passes_unknown_ones_through(double value, string unit, string expected)
        {
            Assert.That(Units.Format(value, unit), Is.EqualTo(expected));
        }

        [Test]
        public void Formats_with_the_invariant_culture_under_a_comma_decimal_culture()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");

                Assert.That(Units.FormatBytes(12480102), Is.EqualTo("11.9 MB"));
                Assert.That(Units.Format(12.34, Units.Milliseconds), Is.EqualTo("12.3 ms"));
                Assert.That(Units.Format(1026, Units.Count), Is.EqualTo("1,026"));
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [Test]
        public void A_measurement_prints_through_the_same_formatter()
        {
            Assert.That(Measurement.Bytes(12480102).ToString(), Is.EqualTo("11.9 MB"));
            Assert.That(Measurement.Count(62).ToString(), Is.EqualTo("62"));
        }

        [Test]
        public void A_measurement_needs_a_unit()
        {
            Assert.That(() => new Measurement(1, ""), Throws.ArgumentException);
            Assert.That(() => new Measurement(1, null), Throws.ArgumentException);
        }
    }
}
