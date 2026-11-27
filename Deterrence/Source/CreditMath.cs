using System;

namespace Deterrence
{
    public enum AccumulationCurve { Logarithmic, SquareRoot }
    public enum FadeCurve { Cubic, Linear }

    // Both modes use the same gain law. Keep raw exposure and the rate-weighted
    // clock separately so an edit or a change in weather never rewrites earned points.
    public readonly struct HazardGrowth
    {
        public const double ReferenceRate = 2000;
        public readonly bool ByRate;
        public readonly double Scale, Days, Shape;
        public HazardGrowth(bool byRate, double scale, double days, double shape)
        {
            ByRate = byRate;
            Scale = Math.Max(1, CreditMath.Positive(scale, 20000));
            Days = Math.Max(0.01, CreditMath.Positive(days, 10));
            Shape = Math.Min(1, CreditMath.Positive(shape, 1));
        }

        public static double ClockSpeed(double rate) => rate > 0 ? Math.Sqrt(ReferenceRate / rate) : 0;
        private double Age(double exposure, double clock) => ByRate ? clock / Days : exposure / Scale;
        public double DailyRate(double rate, double exposure, double clock)
            => Shape == 0 ? rate : rate * Math.Pow(1 + Age(exposure, clock) / Shape, -Shape);

        public double Earn(double rate, double days, double exposure, double clock, out double area)
        {
            area = 0;
            if (rate <= 0 || days <= 0) return 0;
            double raw = rate * days;
            if (Shape == 0) { area = raw * days / 2; return raw; }
            double age = Age(exposure, clock);
            double step = ByRate ? days * ClockSpeed(rate) / Days : raw / Scale;
            double z = step / (Shape + age);
            double initial = rate * Math.Pow(1 + age / Shape, -Shape);
            // Integrate the gain and its area over a constant-rate observation interval.
            double mean, weighted;
            if (z < 0.0001)
            {
                mean = 1 - Shape * z / 2 + Shape * (Shape + 1) * z * z / 6;
                weighted = 0.5 - Shape * z / 6 + Shape * (Shape + 1) * z * z / 24;
            }
            else
            {
                double log = CreditMath.LogOnePlus(z);
                double q = 1 - Shape;
                mean = q == 0 ? log / z : CreditMath.ExpMinusOne(q * log) / (q * z);
                weighted = ((1 + z) * mean - 1) / (z * (2 - Shape));
            }
            area = initial * days * days * weighted;
            return initial * days * mean;
        }

        public double FromZero(double rate, double days) => Earn(rate, days, 0, 0, out _);

        // Maintained stocks such as remains pass their raw total through the exposure-scale curve.
        public double Stock(double raw)
            => CreditMath.Finite(raw) && raw > 0 ? new HazardGrowth(false, Scale, Days, Shape).Earn(raw, 1, 0, 0, out _) : 0;
    }

    // No RimWorld dependencies: also compiled directly into the test harness.
    public static class CreditMath
    {
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        public static double Positive(double value, double fallback = 0)
            => Finite(value) && value >= 0 ? value : fallback;

        public static double Accumulated(double exposure, double scale, AccumulationCurve curve)
        {
            exposure = Positive(exposure);
            scale = Math.Max(1, Positive(scale, 20000));
            if (curve == AccumulationCurve.SquareRoot)
            {
                // Rationalized to retain precision for small exposure.
                return 2 * exposure / (Math.Sqrt(1 + 2 * exposure / scale) + 1);
            }
            return scale * LogOnePlus(exposure / scale);
        }

        public static double Increment(double exposure, double added, double scale, AccumulationCurve curve)
        {
            exposure = Positive(exposure);
            added = Positive(added);
            scale = Math.Max(1, Positive(scale, 20000));
            if (curve == AccumulationCurve.SquareRoot)
                return 2 * added / (Math.Sqrt(1 + 2 * (exposure + added) / scale)
                    + Math.Sqrt(1 + 2 * exposure / scale));
            return scale * LogOnePlus(added / (scale + exposure));
        }

        internal static double ExpMinusOne(double x)
            => Math.Abs(x) < 0.00001 ? x * (1 + x * (0.5 + x / 6)) : Math.Exp(x) - 1;

        internal static double LogOnePlus(double x)
        {
            if (x < 0.00001) return x * (1 - x * (0.5 - x / 3));
            return Math.Log(1 + x);
        }

        public static double Duration(double amount, double referenceDays, double referenceAmount, double exponent = 0.5)
            => Math.Max(0.01, Positive(referenceDays, 10))
                * Math.Pow(Positive(amount) / Math.Max(1, Positive(referenceAmount, 10000)),
                    Math.Max(0, Math.Min(1, Positive(exponent, 0.5))));

        public static double Remaining(double original, double elapsedDays, double durationDays, FadeCurve curve = FadeCurve.Cubic)
        {
            original = Positive(original);
            if (durationDays <= 0 || elapsedDays >= durationDays) return 0;
            if (elapsedDays <= 0) return original;
            double u = elapsedDays / durationDays;
            if (curve == FadeCurve.Linear) return original * (1 - u);
            // Equivalent to 1-3u^2+2u^3; stable near the end.
            return original * (1 - u) * (1 - u) * (1 + 2 * u);
        }

        public static float EffectiveWealth(float wealth, double credit)
        {
            if (!Finite(wealth) || wealth < 0) return wealth;
            return (float)Math.Max(0, wealth - Positive(credit));
        }
    }

    public class CreditState
    {
        public double Exposure;
        public double RateClock, ActiveArea;
        public bool HasArea;
        public double Balance;
        public bool Active;
        public double EndDay;
        public double DecayFrom;
        public double DecayDuration;
        public FadeCurve DecayCurve;

        public void Advance(double day)
        {
            if (!Active) Balance = CreditMath.Remaining(DecayFrom, day - EndDay, DecayDuration, DecayCurve);
        }

        public void Activate(double day)
        {
            Advance(day);
            if (!Active) { ActiveArea = 0; HasArea = true; }
            Active = true;
        }

        public void Accrue(double rate, double days, HazardGrowth growth)
        {
            if (rate <= 0 || days <= 0) return;
            double gain = growth.Earn(rate, days, Exposure, RateClock, out double newArea);
            ActiveArea += Balance * days + newArea;
            Balance += gain;
            Exposure += rate * days;
            RateClock += days * HazardGrowth.ClockSpeed(rate);
        }

        public void Accumulate(double rawCredit, double scale, AccumulationCurve curve)
        {
            rawCredit = CreditMath.Positive(rawCredit);
            Balance += CreditMath.Increment(Exposure, rawCredit, scale, curve);
            Exposure += rawCredit;
        }

        public void End(double day, double referenceDays, double referenceAmount,
            double exponent = 0.5, FadeCurve curve = FadeCurve.Cubic, bool useArea = false)
        {
            if (!Active) return;
            Active = false;
            EndDay = day;
            DecayFrom = Balance;
            DecayDuration = CreditMath.Duration(useArea ? ActiveArea : Balance, referenceDays, referenceAmount, exponent);
            DecayCurve = curve;
        }

        public bool Bump(double day, double amount, double referenceDays, double referenceAmount,
            double exponent = 0.5, FadeCurve curve = FadeCurve.Cubic)
        {
            amount = CreditMath.Positive(amount);
            if (amount <= 0) return false; // Disabled/zero bumps cannot renew a reputation.
            Advance(day);
            Balance += amount;
            Active = true;
            End(day, referenceDays, referenceAmount, exponent, curve);
            return true;
        }
    }
}
