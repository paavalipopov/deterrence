using System;

namespace Antiwealth
{
    public class WealthValues
    {
        public int MapId;
        public float Day;
        public float Economy;
        public float VanillaThreat;
        public float SurfaceDiscount;
        public float AdjustedThreat;
        public float Antiwealth;
    }

    public static class HistoryMath
    {
        public static bool Due(double previousDay, double day, bool force)
            => force || day - previousDay >= 0.5;

        public static bool SameTimestamp(double first, double second)
            => Math.Abs(first - second) < 0.00001;
    }
}
