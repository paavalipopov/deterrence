using System;

namespace Antiwealth
{
    // Shared-model simulations for the settings laboratory and regression tests.
    public static class PreviewMath
    {
        public static double Ongoing(double day, double start, double end, double rate, double scale,
            AccumulationCurve growth, double fadeDays, double reference, double exponent, FadeCurve fade)
        {
            if (day <= start) return 0;
            double earned = CreditMath.Accumulated(rate * Math.Max(0, Math.Min(day, end) - start), scale, growth);
            if (day < end) return earned;
            return CreditMath.Remaining(earned, day - end, CreditMath.Duration(earned, fadeDays, reference, exponent), fade);
        }

        public static double Bumps(double day, double amount, double interval, int count,
            double fadeDays, double reference, double exponent, FadeCurve fade)
        {
            var meter = new CreditState();
            for (int i = 0; i < Math.Max(0, Math.Min(20, count)); i++)
            {
                double time = i * Math.Max(0.1, interval);
                if (time > day) break;
                meter.Bump(time, amount, fadeDays, reference, exponent, fade);
            }
            meter.Advance(day);
            return meter.Balance;
        }
    }
}
