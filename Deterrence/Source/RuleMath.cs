using System;

namespace Deterrence
{
    // Shared by the game settings and the independent test harness.
    public class RuleValues
    {
        public string Key;
        public bool Enabled = true;
        public float BaseAmount;
        public float PopulationExponent;
        public int ReferencePopulation = 5;

        public double AmountFor(int colonists)
        {
            return CreditMath.Positive(BaseAmount) * PopulationFactor(colonists);
        }

        public double PopulationFactor(int colonists)
        {
            if (!Enabled) return 0;
            double exponent = Math.Max(0, Math.Min(2, CreditMath.Positive(PopulationExponent)));
            if (exponent == 0) return 1;
            if (colonists <= 0) return 0;
            return Math.Pow((double)colonists / Math.Max(1, ReferencePopulation), exponent);
        }

        public void SanitizeValues()
        {
            BaseAmount = (float)Math.Min(1000000, CreditMath.Positive(BaseAmount));
            PopulationExponent = (float)Math.Min(2, CreditMath.Positive(PopulationExponent));
            ReferencePopulation = Math.Max(1, Math.Min(1000, ReferencePopulation));
        }
    }

    public static class SourceMath
    {
        public const float MaxBiomeExponent = 3;
        public static double RitualReward(bool positiveOutcome, RuleValues rule, int population)
            => positiveOutcome ? rule.AmountFor(population) : 0;

        public static double SurfaceDiscount(double countedThreatWealth, double multiplier)
            => CreditMath.Positive(countedThreatWealth) * (1 - Math.Min(1, CreditMath.Positive(multiplier, 1)));

        public static double KeptOrgans(double unitMarketValue, int count, double multiplier)
            => CreditMath.Positive(unitMarketValue) * Math.Max(0, count)
                * Math.Min(5, CreditMath.Positive(multiplier, 0.75));

        public static double MaintainedRemains(double power, RuleValues rule, int population)
            => CreditMath.Positive(power) * rule.AmountFor(population);

        public static double DeathBump(double power, RuleValues rule, int population, double gearValue, double gearFraction)
            => Remains(power, rule, population, gearValue, gearFraction, 1);
        public static double Remains(double power, RuleValues rule, int population,
            double gearValue, double gearFraction, double preservation)
            => (CreditMath.Positive(power) * CreditMath.Positive(rule.BaseAmount)
                + CreditMath.Positive(gearValue) * Math.Min(1, CreditMath.Positive(gearFraction)))
                * rule.PopulationFactor(population) * Math.Min(1, CreditMath.Positive(preservation));

        public static double Pollution(double fullMapRate, double fraction)
            => CreditMath.Positive(fullMapRate) * Math.Max(0, Math.Min(1, CreditMath.Positive(fraction)));

        public static double HeatExposure(double temperature, double threshold)
            => CreditMath.Finite(temperature) && CreditMath.Finite(threshold) ? Math.Max(0, (temperature - threshold) / 10) : 0;

        public static double ColdExposure(double temperature, double threshold)
            => CreditMath.Finite(temperature) && CreditMath.Finite(threshold) ? Math.Max(0, (threshold - temperature) / 10) : 0;

        public static double AdjustedThreatWealth(float gameThreatWealth, double deterrence,
            bool deterrenceEnabled, double biomeFactor, double islandFactor = 1)
            => CreditMath.EffectiveWealth(gameThreatWealth, deterrenceEnabled ? deterrence : 0)
                * CreditMath.Positive(biomeFactor, 1) * CreditMath.Positive(islandFactor, 1);

        public static double IslandFactor(bool isIsland, double multiplier)
            => isIsland ? Math.Min(1, CreditMath.Positive(multiplier, 1)) : 1;

        public static string ThreatWealthFormula(bool deterrenceEnabled, bool biomeEnabled, bool islandEnabled)
        {
            string threat = deterrenceEnabled ? "max(0, Base - Deterrence)" : "Base";
            if (biomeEnabled) threat += " × biome factor";
            if (islandEnabled) threat += " × island factor";
            return "Threat = " + threat;
        }

        public static float AdjustRaidBudget(float budget, float globalPoints, float factionPoints)
        {
            if (!CreditMath.Finite(budget) || !CreditMath.Finite(globalPoints) || !CreditMath.Finite(factionPoints)
                || budget <= 0 || globalPoints <= 0 || factionPoints <= 0 || globalPoints == factionPoints) return budget;
            // Keep any existing storyteller multiplier, including Randy's random factor.
            double adjusted = budget * ((double)factionPoints / globalPoints);
            return adjusted <= float.MaxValue ? (float)adjusted : budget;
        }

        // Share of Base that still counts as threat: 1 without deterrence, 0 when nothing counts.
        public static double ThreatRatio(double threat, double baseWealth)
            => CreditMath.Finite(threat) && CreditMath.Finite(baseWealth) && baseWealth > 0
                ? Math.Max(0, Math.Min(1, threat / baseWealth)) : 1;

        public static double SkipChance(double ratio, double strength, double exponent)
            => Math.Min(1, CreditMath.Positive(strength))
                * Math.Pow(1 - Math.Max(0, Math.Min(1, ratio)), Math.Max(0.01, CreditMath.Positive(exponent, 2)));

        // Raiders retreat after a smaller share of losses as reputation approaches the reference amount.
        public static double RetreatFraction(double vanilla, double reputation, double reference, double factor)
        {
            double reach = Math.Min(1, CreditMath.Positive(reputation) / Math.Max(1, CreditMath.Positive(reference, 10000)));
            double target = Math.Max(0, Math.Min(1, CreditMath.Positive(factor, 1)));
            return CreditMath.Positive(vanilla) * (1 - (1 - target) * reach);
        }

        public static double BiomeMultiplier(double settlementWeight, double exponent, bool enabled)
        {
            if (!enabled) return 1;
            exponent = Math.Min(MaxBiomeExponent, CreditMath.Positive(exponent, 0.5));
            if (exponent == 0) return 1;
            return Math.Pow(Math.Min(1, CreditMath.Positive(settlementWeight, 1)), exponent);
        }
    }
}
