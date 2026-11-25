using System;
using System.Collections.Generic;
using System.Linq;

namespace Deterrence
{
    public class ShareValues
    {
        public string Key, Label, SourceKey;
        public double Amount, Day;
    }

    // Attribution only: the shared meter remains the sole source of actual credit.
    public static class ShareMath
    {
        public static bool Add<T>(List<T> shares, double remaining, T next, Func<T> earlier, int recentLimit = 40) where T : ShareValues
        {
            if (next == null || CreditMath.Positive(next.Amount) <= 0) return false;
            remaining = CreditMath.Positive(remaining);
            double sum = shares.Sum(s => CreditMath.Positive(s.Amount));
            if (remaining == 0) shares.Clear();
            else if (sum > 0)
                foreach (var share in shares) share.Amount = CreditMath.Positive(share.Amount) / sum * remaining;
            else
            {
                shares.Clear();
                var old = earlier(); old.Amount = remaining; shares.Add(old);
            }
            shares.Add(next);
            recentLimit = Math.Max(1, recentLimit);
            if (shares.Count > recentLimit + (shares[0].Key == "earlier" ? 1 : 0))
            {
                if (shares[0].Key != "earlier") shares.Insert(0, earlier());
                while (shares.Count > recentLimit + 1)
                {
                    shares[0].Amount += CreditMath.Positive(shares[1].Amount);
                    shares.RemoveAt(1);
                }
            }
            return true;
        }

        public static double Current(double weight, double totalWeight, double currentBalance)
            => totalWeight > 0 ? CreditMath.Positive(weight) / totalWeight * CreditMath.Positive(currentBalance) : 0;
    }
}
