using System;

namespace ClarityGameOptimizer.Core
{
    /// <summary>A number with its unit. The budget it belongs to lives on the finding or metric that carries it.</summary>
    internal readonly struct Measurement
    {
        public Measurement(double value, string unit)
        {
            if (string.IsNullOrEmpty(unit))
            {
                throw new ArgumentException("A measurement needs a unit.", nameof(unit));
            }

            Value = value;
            Unit = unit;
        }

        public double Value { get; }

        public string Unit { get; }

        public static Measurement Bytes(double value)
        {
            return new Measurement(value, Units.Bytes);
        }

        public static Measurement Milliseconds(double value)
        {
            return new Measurement(value, Units.Milliseconds);
        }

        public static Measurement Seconds(double value)
        {
            return new Measurement(value, Units.Seconds);
        }

        public static Measurement Count(double value)
        {
            return new Measurement(value, Units.Count);
        }

        public static Measurement Percent(double value)
        {
            return new Measurement(value, Units.Percent);
        }

        public override string ToString()
        {
            return Units.Format(Value, Unit);
        }
    }
}
