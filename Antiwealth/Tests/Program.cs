using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Antiwealth;

internal static class Program
{
    private static int passed;
    private static void Check(string name, Action test)
    {
        test();
        passed++;
        Console.WriteLine("PASS " + name);
    }

    private static void Equal(double expected, double actual, double tolerance = 0.000001)
    {
        if (!CreditMath.Finite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new Exception("Expected " + expected + ", got " + actual);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--export-violence")
        {
            ViolenceExample.Write(args[1]);
            return;
        }
        if (args.Length == 2 && args[0] == "--export-example")
        {
            ExampleExport.Write(args[1]);
            return;
        }
        Check("exposure mode reproduces linear, square root and logarithmic growth", () =>
        {
            foreach (double rate in new[] { 1.0, 1000, 4000 })
            {
                Equal(rate * 8, new HazardGrowth(false, 20000, 10, 0).FromZero(rate, 8));
                Equal(CreditMath.Accumulated(rate * 8, 20000, AccumulationCurve.Logarithmic),
                    new HazardGrowth(false, 20000, 10, 1).FromZero(rate, 8));
                Equal(CreditMath.Accumulated(rate * 8, 20000, AccumulationCurve.SquareRoot),
                    new HazardGrowth(false, 20000, 10, 0.5).FromZero(rate, 8));
            }
        });
        Check("buildup-rate clock reaches half speed at the designed day", () =>
        {
            foreach (double rate in new[] { 1000.0, 2000, 4000 })
            foreach (double p in new[] { 0.5, 1 })
            {
                var growth = new HazardGrowth(true, 20000, 10, p);
                double halfDay = 10 * Math.Sqrt(rate / 2000) * (p == 1 ? 1 : 1.5);
                var state = new CreditState();
                state.Accrue(rate, halfDay, growth);
                Equal(rate / 2, growth.DailyRate(rate, state.Exposure, state.RateClock));
            }
        });
        Check("gains and area are independent of sampling at changing rates and settings", () =>
        {
            foreach (bool byRate in new[] { true, false })
            foreach (double p in new[] { 0, 0.01, 0.5, 0.77, 0.9999999, 1 })
            {
                var whole = new CreditState { Balance = 123 };
                var split = new CreditState { Balance = 123 };
                foreach (double rate in new[] { 50.0, 15000, 300 })
                {
                    var growth = new HazardGrowth(byRate, 20000, 10, p);
                    whole.Accrue(rate, 3, growth);
                    for (int i = 0; i < 72; i++) split.Accrue(rate, 1.0 / 24, growth);
                    Equal(whole.Balance, split.Balance, 0.00001);
                    Equal(whole.ActiveArea, split.ActiveArea, 0.00001);
                    Equal(whole.RateClock, split.RateClock);
                }
            }
        });
        Check("area fades use point-days of the active episode and freeze on ending", () =>
        {
            foreach (double days in new[] { 2.0, 8 })
            {
                var state = new CreditState();
                state.Activate(0);
                state.Accrue(10000 / days, days, new HazardGrowth(false, 20000, 10, 0));
                Equal(10000, state.Balance);
                Equal(5000 * days, state.ActiveArea);
                state.End(days, 10, 20000, useArea: true);
                Equal(10 * Math.Sqrt(days / 4), state.DecayDuration);
                state.Activate(days + state.DecayDuration / 2);
                Equal(5000, state.Balance);
                Equal(0, state.ActiveArea);
                Equal(10000, state.Exposure);
            }
        });
        foreach (AccumulationCurve curve in Enum.GetValues<AccumulationCurve>())
        {
            Check(curve + ": accumulation is independent of sample frequency", () =>
            {
                var daily = new CreditState { Active = true };
                var hourly = new CreditState { Active = true };
                for (int d = 0; d < 120; d++) daily.Accumulate(4000, 20000, curve);
                for (int h = 0; h < 120 * 24; h++) hourly.Accumulate(4000.0 / 24, 20000, curve);
                Equal(CreditMath.Accumulated(480000, 20000, curve), daily.Balance);
                Equal(daily.Balance, hourly.Balance, 0.00001);
            });
            Check(curve + ": growth is diminishing and unbounded", () =>
            {
                double last = 0, previousGain = double.PositiveInfinity;
                for (int i = 0; i < 100; i++)
                {
                    double value = CreditMath.Accumulated(i * 1000, 20000, curve);
                    double gain = CreditMath.Increment(i * 1000, 1000, 20000, curve);
                    Require(value >= last && gain > 0 && gain < previousGain, "Growth or concavity broken");
                    previousGain = gain; last = value;
                }
                Require(CreditMath.Accumulated(1e30, 20000, curve) > last * 2, "Unexpected cap");
            });
        }
        Check("logarithmic accumulation diminishes more strongly", () =>
            Require(CreditMath.Accumulated(400000, 20000, AccumulationCurve.Logarithmic)
                < CreditMath.Accumulated(400000, 20000, AccumulationCurve.SquareRoot), "Curve order"));
        Check("square-root duration matches calibration and quadrupling", () =>
        {
            Equal(10, CreditMath.Duration(10000, 10, 10000));
            Equal(20, CreditMath.Duration(40000, 10, 10000));
            Equal(5, CreditMath.Duration(2500, 10, 10000));
        });
        Check("cubic recovery starts full, halves at midpoint, ends exactly", () =>
        {
            Equal(10000, CreditMath.Remaining(10000, 0, 10));
            Equal(5000, CreditMath.Remaining(10000, 5, 10));
            Equal(0, CreditMath.Remaining(10000, 10, 10));
            Equal(0, CreditMath.Remaining(10000, 20, 10));
        });
        Check("cubic recovery is monotone, nonnegative, and smooth at both ends", () =>
        {
            double previous = 10000;
            for (int i = 0; i <= 10000; i++)
            {
                double current = CreditMath.Remaining(10000, i / 1000.0, 10);
                Require(current >= 0 && current <= previous, "Invalid cubic recovery");
                previous = current;
            }
            Require(10000 - CreditMath.Remaining(10000, 0.001, 10) < 0.001, "Abrupt start");
            Require(CreditMath.Remaining(10000, 9.999, 10) < 0.001, "Abrupt end");
        });
        Check("repeated ending does not restart recovery", () =>
        {
            var state = new CreditState { Balance = 10000, Active = true };
            state.End(30, 10, 10000);
            state.Advance(35);
            Equal(5000, state.Balance);
            state.End(35, 10, 10000);
            state.Advance(40);
            Equal(0, state.Balance);
        });
        Check("resuming retains remaining credit and prior diminishing returns", () =>
        {
            var state = new CreditState { Active = true };
            state.Accumulate(40000, 20000, AccumulationCurve.Logarithmic);
            state.End(10, 10, 10000);
            double mid = 10 + state.DecayDuration / 2;
            double half = state.DecayFrom / 2;
            state.Activate(mid);
            Equal(half, state.Balance);
            Equal(40000, state.Exposure);
            state.Accumulate(4000, 20000, AccumulationCurve.Logarithmic);
            Equal(half + CreditMath.Increment(40000, 4000, 20000, AccumulationCurve.Logarithmic), state.Balance);
            Require(state.Balance - half < CreditMath.Increment(0, 4000, 20000, AccumulationCurve.Logarithmic), "Reset exploit");
        });
        Check("serialized state resumes the same recovery trajectory", () =>
        {
            var state = new CreditState { Active = true, Balance = 25000, Exposure = 35000 };
            state.End(9, 10, 10000);
            state.Advance(12);
            var options = new JsonSerializerOptions { IncludeFields = true };
            var restored = JsonSerializer.Deserialize<CreditState>(JsonSerializer.Serialize(state, options), options);
            state.Advance(17);
            restored.Advance(17);
            Equal(state.Balance, restored.Balance);
            Equal(state.Exposure, restored.Exposure);
            Equal(state.EndDay, restored.EndDay);
        });
        Check("changing growth rules never rewrites existing credit", () =>
        {
            var state = new CreditState { Active = true };
            state.Accumulate(40000, 20000, AccumulationCurve.Logarithmic);
            double old = state.Balance;
            state.Accumulate(0, 20000, AccumulationCurve.SquareRoot);
            Equal(old, state.Balance);
            state.Accumulate(1000, 20000, AccumulationCurve.SquareRoot);
            Equal(old + CreditMath.Increment(40000, 1000, 20000, AccumulationCurve.SquareRoot), state.Balance);
        });
        Check("threat credit cannot make wealth negative or spend itself on queries", () =>
        {
            Equal(80000, CreditMath.EffectiveWealth(100000, 20000));
            Equal(80000, CreditMath.EffectiveWealth(100000, 20000));
            Equal(0, CreditMath.EffectiveWealth(100000, 200000));
            Equal(100000, CreditMath.EffectiveWealth(100000, -100));
            Equal(100000, CreditMath.EffectiveWealth(100000, double.NaN));
        });
        Check("disabled growth rates add no credit", () =>
        {
            var state = new CreditState { Active = true, Exposure = 50000, Balance = 1234 };
            state.Accumulate(0, 20000, AccumulationCurve.Logarithmic);
            Equal(1234, state.Balance);
            Equal(50000, state.Exposure);
        });
        Check("fixed event rates preserve version 0.1 behavior at any population", () =>
        {
            var rule = new RuleValues { BaseAmount = 4000, PopulationExponent = 0, ReferencePopulation = 5 };
            foreach (int count in new[] { 0, 1, 5, 10, 100 }) Equal(4000, rule.AmountFor(count));
        });
        Check("linear scaling is calibrated at the reference population", () =>
        {
            var rule = new RuleValues { BaseAmount = 4000, PopulationExponent = 1, ReferencePopulation = 5 };
            Equal(800, rule.AmountFor(1)); Equal(4000, rule.AmountFor(5)); Equal(8000, rule.AmountFor(10));
            Equal(16000, rule.AmountFor(20)); Equal(0, rule.AmountFor(0));
        });
        Check("square-root population scaling grows more slowly than linear", () =>
        {
            var rule = new RuleValues { BaseAmount = 4000, PopulationExponent = 0.5f, ReferencePopulation = 5 };
            Equal(4000, rule.AmountFor(5)); Equal(8000, rule.AmountFor(20));
            Require(rule.AmountFor(20) < 16000, "Population scaling not sublinear");
        });
        Check("disabled sources stop generating new credit", () =>
        {
            var rule = new RuleValues { BaseAmount = 4000, Enabled = false, PopulationExponent = 1 };
            Equal(0, rule.AmountFor(5)); Equal(0, rule.AmountFor(100));
        });
        Check("separate event rules do not share amounts or population controls", () =>
        {
            var hot = new RuleValues { Key = "condition:HeatWave", BaseAmount = 1500 };
            var cold = new RuleValues { Key = "condition:ColdSnap", BaseAmount = 1500 };
            hot.BaseAmount = 9000; hot.PopulationExponent = 1;
            Equal(1500, cold.AmountFor(20)); Equal(36000, hot.AmountFor(20));
        });
        Check("rule settings round-trip without losing the custom population curve", () =>
        {
            var rule = new RuleValues { Key = "condition:ToxicFallout", BaseAmount = 850, PopulationExponent = 0.75f, ReferencePopulation = 7 };
            var options = new JsonSerializerOptions { IncludeFields = true };
            var restored = JsonSerializer.Deserialize<RuleValues>(JsonSerializer.Serialize(rule, options), options);
            Equal(rule.AmountFor(13), restored.AmountFor(13));
            Require(restored.Key == rule.Key, "Rule identity lost");
        });
        Check("invalid numeric settings cannot produce invalid rates", () =>
        {
            var rule = new RuleValues { BaseAmount = float.NaN, PopulationExponent = float.PositiveInfinity, ReferencePopulation = 0 };
            rule.SanitizeValues(); Equal(0, rule.AmountFor(10));
            Require(rule.ReferencePopulation == 1, "Invalid reference population");
            rule.BaseAmount = 100; rule.PopulationExponent = 1;
            Equal(0, rule.AmountFor(-5));
        });
        Check("population previews and accumulation use the same initial rates", () =>
        {
            var rule = new RuleValues { BaseAmount = 4000, PopulationExponent = 0.5f, ReferencePopulation = 5 };
            var state = new CreditState { Active = true };
            double rate = rule.AmountFor(20);
            for (int i = 0; i < 240; i++) state.Accumulate(rate / 24, 20000, AccumulationCurve.Logarithmic);
            Equal(CreditMath.Accumulated(rate * 10, 20000, AccumulationCurve.Logarithmic), state.Balance, 0.00001);
        });
        Check("hostile-death bumps include gear independently of base combat value", () =>
        {
            var rule = new RuleValues { Key = "remains:human", BaseAmount = 10 };
            Equal(2000, SourceMath.DeathBump(50, rule, 5, 1500, 1));
            Equal(1250, SourceMath.DeathBump(50, rule, 5, 1500, 0.5));
            Equal(500, SourceMath.DeathBump(50, rule, 5, 1500, 0));
        });
        Check("gear-only death bumps work with zero combat-power multiplier", () =>
        {
            var rule = new RuleValues { BaseAmount = 0 };
            Equal(1500, SourceMath.DeathBump(50, rule, 5, 1500, 1));
            rule.Enabled = false;
            Equal(0, SourceMath.DeathBump(50, rule, 5, 1500, 1));
        });
        Check("population applies to death bumps and bodies separately", () =>
        {
            var rule = new RuleValues { BaseAmount = 5, PopulationExponent = 1, ReferencePopulation = 5 };
            Equal(500, SourceMath.MaintainedRemains(50, rule, 10));
            Equal(3500, SourceMath.DeathBump(50, rule, 10, 1500, 1));
        });
        Check("pollution is fractional exposure with safe endpoints", () =>
        {
            Equal(800, SourceMath.Pollution(4000, 0.2));
            Equal(0, SourceMath.Pollution(4000, 0));
            Equal(4000, SourceMath.Pollution(4000, 5));
            Equal(0, SourceMath.Pollution(4000, double.NaN));
        });
        Check("extreme temperature credit starts at the chosen threshold", () =>
        {
            Equal(0, SourceMath.HeatExposure(40, 40));
            Equal(1.5, SourceMath.HeatExposure(55, 40));
            Equal(0, SourceMath.ColdExposure(-10, -10));
            Equal(2, SourceMath.ColdExposure(-30, -10));
        });
        Check("biome square-root scaling matches the proposed examples", () =>
        {
            Equal(1, SourceMath.BiomeMultiplier(1, 0.5, true));
            Equal(Math.Sqrt(0.6), SourceMath.BiomeMultiplier(0.6, 0.5, true));
            Equal(0.1, SourceMath.BiomeMultiplier(0.01, 0.5, true));
            Require(SourceMath.BiomeMultiplier(0.6, 0.25, true) > SourceMath.BiomeMultiplier(0.6, 0.5, true), "Quarter power should be milder");
        });
        Check("biome switches, zero weight, and invalid values are safe", () =>
        {
            Equal(1, SourceMath.BiomeMultiplier(0, 0.5, false));
            Equal(1, SourceMath.BiomeMultiplier(0, 0, true));
            Equal(0, SourceMath.BiomeMultiplier(0, 0.5, true));
            Equal(1, SourceMath.BiomeMultiplier(double.NaN, 0.5, true));
            Equal(1, SourceMath.BiomeMultiplier(-3, 0.5, true));
            Equal(1, SourceMath.BiomeMultiplier(4, 0.5, true));
            Equal(0.125, SourceMath.BiomeMultiplier(0.5, 3, true));
            Equal(0.125, SourceMath.BiomeMultiplier(0.5, 4, true));
        });
        Check("biome multiplier applies after antiwealth without changing the base", () =>
        {
            float wealth = 200000;
            Equal(90000, CreditMath.EffectiveWealth(wealth, 20000) * SourceMath.BiomeMultiplier(0.25, 0.5, true));
            Equal(200000, wealth);
            Equal(0, CreditMath.EffectiveWealth(wealth, 300000) * SourceMath.BiomeMultiplier(0.25, 0.5, true));
        });
        Check("history keeps late-start timestamps and values through serialization", () =>
        {
            var sample = new WealthValues { MapId = 7, Day = 137.5f, Economy = 200000, VanillaThreat = 180000, SurfaceDiscount = 3000, AdjustedThreat = 80000, Antiwealth = 20000 };
            var options = new JsonSerializerOptions { IncludeFields = true };
            var restored = JsonSerializer.Deserialize<WealthValues>(JsonSerializer.Serialize(sample, options), options);
            Equal(137.5, restored.Day); Equal(80000, restored.AdjustedThreat); Equal(7, restored.MapId);
            Equal(177000, CreditMath.EffectiveWealth(restored.VanillaThreat, restored.SurfaceDiscount));
        });
        Check("history samples on the half-day interval and identifies paused duplicates", () =>
        {
            Require(!HistoryMath.Due(137.5, 137.75, false), "Early sample");
            Require(HistoryMath.Due(137.5, 138, false), "Missing scheduled sample");
            Require(HistoryMath.Due(137.5, 137.5, true), "Settings changes should be recordable");
            Require(HistoryMath.SameTimestamp(137.5, 137.5), "Paused samples should replace the same point");
            Require(!HistoryMath.SameTimestamp(137.5, 138), "Distinct days collapsed");
        });
        Check("settings group persistent and episodic sources by their mechanic", () =>
        {
            foreach (string key in new[] { "pollution:ground", "climate:heat", "climate:cold" })
                Require(RuleGrouping.SectionFor(key) == SettingsSection.Hazards, "Wrong habitat group: " + key);
            foreach (string key in new[] { "condition:ToxicFallout", "infestation:hives" })
                Require(RuleGrouping.SectionFor(key) == SettingsSection.Hazards, "Wrong event group: " + key);
            Require(RuleGrouping.SectionFor("remains:human") == SettingsSection.Remains, "Remains routing");
            Require(!RuleGrouping.IsSupported("illness:Flu"), "Illness must not be selectable");
        });
        Check("menu section order follows the description", () =>
        {
            string actual = string.Join(",", Enum.GetNames<SettingsSection>());
            Require(actual == "Formula,Remains,Reputation,Hazards,Curves,Summary,Misc,Presets", "Documentation/menu order mismatch");
        });
        Check("illness migration removes active and recovering legacy sources only", () =>
        {
            Require(RuleGrouping.IsIllness("illness:42:123"), "Active illness not recognized");
            Require(RuleGrouping.IsIllness("old-format-key", "Illness"), "Legacy category not recognized");
            Require(RuleGrouping.IsIllness("ILLNESS:Flu"), "Legacy case handling");
            Require(!RuleGrouping.IsIllness("remains:42", "Remains"), "Scaria/manhunter remains must remain eligible");
            Require(!RuleGrouping.IsIllness("pollution:7", "Environment"), "Pollution must remain eligible");
        });
        Check("removed source rules cannot be selected", () =>
        {
            foreach (string key in new[] { "condition:SolarFlare", "condition:Eclipse", "condition:PsychicDrone",
                "condition:VolcanicWinter", "condition:HeatWave", "condition:ColdSnap", "terrain:poor", "terrain:swamp", "illness:Flu" })
                Require(!RuleGrouping.IsSupported(key), "Removed rule supported: " + key);
            Require(RuleGrouping.SectionFor("bump:organ") == SettingsSection.Reputation, "Violence routing");
        });
        Check("bodies use combat power alone", () =>
        {
            var rule = new RuleValues { BaseAmount = 5 };
            Equal(250, SourceMath.MaintainedRemains(50, rule, 5));
            rule.Enabled = false;
            Equal(0, SourceMath.MaintainedRemains(50, rule, 5));
        });
        Check("positive bumps add only remaining credit and renew the fade", () =>
        {
            var meter = new CreditState();
            meter.Bump(0, 10000, 10, 10000);
            meter.Advance(5);
            Equal(5000, meter.Balance);
            meter.Bump(5, 2000, 10, 10000);
            Equal(7000, meter.Balance);
            Equal(5, meter.EndDay);
            Equal(CreditMath.Duration(7000, 10, 10000), meter.DecayDuration);
            Require(!meter.Active, "A bump must immediately fade");
        });
        Check("zero and disabled bumps cannot refresh old reputation", () =>
        {
            var meter = new CreditState();
            meter.Bump(0, 10000, 10, 10000);
            Require(!meter.Bump(5, 0, 10, 10000), "Zero bump accepted");
            Equal(0, meter.EndDay);
            meter.Advance(10); Equal(0, meter.Balance);
        });
        Check("a bump after complete fading starts from zero", () =>
        {
            var meter = new CreditState();
            meter.Bump(0, 10000, 10, 10000);
            meter.Bump(20, 2000, 10, 10000);
            Equal(2000, meter.Balance);
        });
        Check("fade shape changes values but not finite endpoints", () =>
        {
            Equal(7500, CreditMath.Remaining(10000, 2.5, 10, FadeCurve.Linear));
            Equal(8437.5, CreditMath.Remaining(10000, 2.5, 10, FadeCurve.Cubic));
            Equal(0, CreditMath.Remaining(10000, 10, 10, FadeCurve.Linear));
        });
        Check("duration exponents support fixed cube-root and square-root scaling", () =>
        {
            Equal(10, CreditMath.Duration(80000, 10, 10000, 0));
            Equal(20, CreditMath.Duration(80000, 10, 10000, 1.0/3));
            Equal(20, CreditMath.Duration(40000, 10, 10000, 0.5));
            Equal(40, CreditMath.Duration(40000, 10, 10000, 1));
        });
        Check("ongoing preview matches the real state through ending and fading", () =>
        {
            var state = new CreditState { Active = true };
            state.Accumulate(24000, 20000, AccumulationCurve.Logarithmic);
            state.End(6, 10, 10000);
            state.Advance(9);
            Equal(state.Balance, PreviewMath.Ongoing(9, 0, 6, 4000, 20000, AccumulationCurve.Logarithmic, 10, 10000, 0.5, FadeCurve.Cubic));
        });
        Check("repeat-bump preview matches actual shared-meter renewals", () =>
        {
            var state = new CreditState();
            state.Bump(0, 2000, 10, 10000);
            state.Bump(3, 2000, 10, 10000);
            state.Bump(6, 2000, 10, 10000);
            state.Advance(7);
            Equal(state.Balance, PreviewMath.Bumps(7, 2000, 3, 3, 10, 10000, 0.5, FadeCurve.Cubic));
        });
        Check("fallout repeats share one typed map meter and violence uses one pool", () =>
        {
            Require(RuleGrouping.ConditionMeter(7,"ToxicFallout") == "condition:7:ToxicFallout", "Incident ID leaked");
            Require(RuleGrouping.ConditionMeter(7,"ToxicFallout") != RuleGrouping.ConditionMeter(8,"ToxicFallout"), "Maps merged");
            Require(RuleGrouping.ReputationMeter(7) == "reputation:violence:7", "Shared violence identity");
        });
        Check("old fades preserve their saved shape and duration", () =>
        {
            var state = new CreditState { Active = true, Balance = 10000 };
            state.End(0, 10, 10000, 0.5, FadeCurve.Linear);
            state.End(5, 60, 10000, 1, FadeCurve.Cubic);
            Equal(10, state.DecayDuration);
            Require(state.DecayCurve == FadeCurve.Linear, "Existing fade was rewritten");
            state.Advance(2.5); Equal(7500, state.Balance);
        });
        Check("failed prisoner surgery earns only its own weight", () =>
        {
            Require(SurgeryOutcome.RuleFor(true, false, false) == "bump:failedSurgery", "Failure not counted");
            Require(SurgeryOutcome.RuleFor(true, true, true) == "bump:failedSurgery", "Failure double-counted");
        });
        Check("successful harvest and non-product amputation are exclusive", () =>
        {
            Require(SurgeryOutcome.RuleFor(false, true, true) == "bump:organ", "Harvest not recognized");
            Require(SurgeryOutcome.RuleFor(false, false, true) == "bump:amputation", "Amputation not counted");
            Require(SurgeryOutcome.RuleFor(false, false, false) == null, "No removal gave free credit");
        });
        Check("kept organs scale with current stock value and quantity", () =>
        {
            Equal(750, SourceMath.KeptOrgans(1000, 1, 0.75));
            Equal(1500, SourceMath.KeptOrgans(1000, 2, 0.75));
            Equal(5000, SourceMath.KeptOrgans(1000, 1, 5));
        });
        Check("organ stock has safe limits and zero controls", () =>
        {
            Equal(0, SourceMath.KeptOrgans(1000, 1, 0));
            Equal(0, SourceMath.KeptOrgans(1000, 0, 0.75));
            Equal(0, SourceMath.KeptOrgans(-1000, 1, 0.75));
            Equal(5000, SourceMath.KeptOrgans(1000, 1, 20));
            Require(RuleGrouping.SectionFor("organs:local") == SettingsSection.Remains, "Organ controls missing from Remains");
        });
        Check("hostile remains groups bodies and organs once and keeps maps separate", () =>
        {
            var values = new[] {
                new LedgerValues { Key="remains:1", SourceKey="remains:human", CorpseId=1, MapId=7, Active=true, Balance=500, Buried=true },
                new LedgerValues { Key="remains:2", SourceKey="remains:animal", CorpseId=2, MapId=7, Active=false, Balance=100, EndDay=0, DecayDuration=10 },
                new LedgerValues { Key="organs:7", SourceKey="organs:local", MapId=7, Active=true, Balance=750 },
                new LedgerValues { Key="reputation:violence:7", Category="Reputation", MapId=7, Balance=2000 },
                new LedgerValues { Key="pollution:7", SourceKey="pollution:ground", Label="Polluted ground (20%)", MapId=7, Active=true, Balance=300 },
                new LedgerValues { Key="remains:8", SourceKey="remains:human", CorpseId=8, MapId=8, Active=true, Balance=9999 }
            };
            var groups = MeterGrouping.Build(values,7);
            Equal(1350, groups[0].Balance);
            Equal(3650, groups.Sum(g=>g.Balance));
            Require(groups[0].Kind == MeterKind.HostileRemains && groups[0].Present==2 && groups[0].Fading==1, "Wrong remains summary");
            var tree = MeterTree.Build(groups, new[] { new OrganStockValues { Key="item:1", Label="Lung", Credit=750, MarketValue=1000, Count=1 } }, 1);
            Equal(3650, MeterTree.All(tree).Where(n=>n.Children.Count==0).Sum(n=>n.Credit));
            Require(MeterTree.All(tree).Any(n=>n.State=="Buried"), "Burial state lost");
        });
        Check("meter expansion shows contributors without changing credit or fades", () =>
        {
            var value = new LedgerValues { Key="remains:3", SourceKey="remains:human", CorpseId=3, MapId=1,
                Active=false, Balance=500, DecayFrom=1000, EndDay=5, DecayDuration=10 };
            var roots = MeterTree.Build(MeterGrouping.Build(new[]{value},1), Array.Empty<OrganStockValues>(), 7);
            var opened = new HashSet<string>();
            Equal(3, MeterTree.Visible(roots,opened).Count);
            opened.Add("hostile-remains"); Equal(4, MeterTree.Visible(roots,opened).Count);
            opened.Add("hostile-remains:human"); Equal(5, MeterTree.Visible(roots,opened).Count);
            Equal(500,value.Balance); Equal(5,value.EndDay); Equal(10,value.DecayDuration);
            Require(!value.Active,"Displaying a source reactivated it");
        });
        Check("environment summaries group surviving legacy entries by source type", () =>
        {
            var values = new[] {
                new LedgerValues { Key="condition:1:old", SourceKey="condition:ToxicFallout", MapId=1, Balance=200 },
                new LedgerValues { Key="condition:1:ToxicFallout", SourceKey="condition:ToxicFallout", MapId=1, Active=true, Balance=500 }
            };
            var groups=MeterGrouping.Build(values,1);
            Equal(3,groups.Count); Equal(700,groups[2].Balance);
            Require(groups[2].Contributors.Count==2,"Legacy contribution omitted");
        });
        Check("violence attribution scales old shares and adds the new act", () =>
        {
            var shares=new List<ShareValues> { new ShareValues { Key="act:1", Amount=10000 } };
            ShareMath.Add(shares,5000,new ShareValues { Key="act:2",Amount=2000 },()=>new ShareValues { Key="earlier",Label="Earlier violence" });
            Equal(5000,shares[0].Amount); Equal(7000,shares.Sum(p=>p.Amount));
            Equal(2500,ShareMath.Current(shares[0].Amount,7000,3500));
        });
        Check("legacy violence retains an honest earlier-violence share", () =>
        {
            var shares=new List<ShareValues>();
            ShareMath.Add(shares,5000,new ShareValues { Key="act:2",Amount=2000 },()=>new ShareValues { Key="earlier",Label="Earlier violence" });
            Require(shares[0].Key=="earlier","Missing legacy placeholder");
            Equal(5000,shares[0].Amount); Equal(7000,shares.Sum(p=>p.Amount));
        });
        Check("bounded violence details conserve the complete balance", () =>
        {
            var shares=new List<ShareValues>();
            for(int i=0;i<100;i++)
            {
                double remaining=shares.Sum(p=>p.Amount)*0.8;
                ShareMath.Add(shares,remaining,new ShareValues { Key="act:"+i,Amount=1000 },()=>new ShareValues { Key="earlier",Label="Earlier violence" },4);
                Equal(remaining+1000,shares.Sum(p=>p.Amount),0.00001);
                Require(shares.Count<=5,"Unbounded detail history");
            }
            Require(shares[0].Key=="earlier","Old contributions were dropped rather than combined");
        });
        Check("zero-value acts leave the attribution unchanged", () =>
        {
            var shares=new List<ShareValues> { new ShareValues { Key="act:1",Amount=1000 } };
            Require(!ShareMath.Add(shares,500,new ShareValues { Amount=0 },()=>new ShareValues()),"Zero detail accepted");
            Equal(1000,shares[0].Amount); Equal(1,shares.Count);
        });
        Check("violence tree shows remaining shares rather than original bumps", () =>
        {
            var entry=new LedgerValues { Key="reputation:violence:1",Category="Reputation",MapId=1,Balance=3500 };
            var parts=new[] { new ShareValues { Key="act:1",Label="First act",Amount=5000 }, new ShareValues { Key="act:2",Label="Second act",Amount=2000 } };
            var roots=MeterTree.Build(MeterGrouping.Build(new[]{entry},1),Array.Empty<OrganStockValues>(),2,e=>parts);
            Equal(3500,roots[1].Children.Sum(n=>n.Credit));
            Equal(1000,roots[1].Children[0].Credit);
        });
        Check("ritual reward applies only to positive enabled outcomes", () =>
        {
            var rule=new RuleValues { Key="bump:ritual",BaseAmount=5000 };
            Equal(5000,SourceMath.RitualReward(true,rule,5));
            Equal(0,SourceMath.RitualReward(false,rule,5));
            rule.Enabled=false; Equal(0,SourceMath.RitualReward(true,rule,5));
        });
        Check("surface multiplier keeps vanilla at one and removes the counted part at zero", () =>
        {
            Equal(0,SourceMath.SurfaceDiscount(2000,1));
            Equal(1000,SourceMath.SurfaceDiscount(2000,0.5));
            Equal(2000,SourceMath.SurfaceDiscount(2000,0));
            Equal(0,SourceMath.SurfaceDiscount(2000,double.NaN));
            Equal(0,SourceMath.SurfaceDiscount(2000,2));
        });
        Check("surface wealth adjustment is separate from deterrence and applies before biome", () =>
        {
            float wealth=200000;
            double discount=SourceMath.SurfaceDiscount(5000,0.4);
            Equal(88500,CreditMath.EffectiveWealth(wealth,20000+discount)*0.5);
            Equal(200000,wealth);
        });
        Check("physical contributors retain map-jump targets", () =>
        {
            var values=new[] { new LedgerValues { Key="remains:3",SourceKey="remains:human",CorpseId=33,MapId=1,Active=true,Balance=500,Buried=true },
                new LedgerValues { Key="organs:1",SourceKey="organs:local",MapId=1,Active=true,Balance=750 }};
            var stock=new[] {new OrganStockValues { Key="item:4",Label="Lung",TargetId=44,Credit=750,MarketValue=1000,Count=1 }};
            var roots=MeterTree.Build(MeterGrouping.Build(values,1),stock,0);
            Require(MeterTree.All(roots).Any(n=>n.TargetId==33 && n.State=="Buried"),"Buried-body target missing");
            Require(MeterTree.All(roots).Any(n=>n.TargetId==44),"Organ target missing");
            Require(roots.Count==3 && roots[2].Label=="Hazards","Main vocabulary groups missing");
        });
        Check("deterrence and biome switches act independently", () =>
        {
            Equal(100000, SourceMath.AdjustedThreatWealth(100000,20000,false,SourceMath.BiomeMultiplier(0.25,0.5,false)));
            Equal(80000, SourceMath.AdjustedThreatWealth(100000,20000,true,SourceMath.BiomeMultiplier(0.25,0.5,false)));
            Equal(50000, SourceMath.AdjustedThreatWealth(100000,20000,false,SourceMath.BiomeMultiplier(0.25,0.5,true)));
            Equal(40000, SourceMath.AdjustedThreatWealth(100000,20000,true,SourceMath.BiomeMultiplier(0.25,0.5,true)));
            Equal(0, SourceMath.AdjustedThreatWealth(100000,200000,true,0.5));
        });
        Check("Misc surface correction remains independent of both formula switches", () =>
        {
            float gameWealth = CreditMath.EffectiveWealth(100000, SourceMath.SurfaceDiscount(10000,0));
            Equal(90000, SourceMath.AdjustedThreatWealth(gameWealth,20000,false,1));
            Equal(70000, SourceMath.AdjustedThreatWealth(gameWealth,20000,true,1));
            Equal(45000, SourceMath.AdjustedThreatWealth(gameWealth,20000,false,0.5));
            Equal(35000, SourceMath.AdjustedThreatWealth(gameWealth,20000,true,0.5));
        });
        Check("faction sizing retains the storyteller multiplier and handles invalid inputs", () =>
        {
            Equal(1500, SourceMath.AdjustRaidBudget(900,600,1000));
            Equal(450, SourceMath.AdjustRaidBudget(900,600,300));
            Equal(900, SourceMath.AdjustRaidBudget(900,600,600));
            Equal(900, SourceMath.AdjustRaidBudget(900,0,600));
            Equal(900, SourceMath.AdjustRaidBudget(900,600,float.NaN));
        });
        Check("island factor multiplies the final wealth independently of biome and deterrence", () =>
        {
            Equal(0.4, SourceMath.IslandFactor(true, 0.4));
            Equal(1, SourceMath.IslandFactor(false, 0));
            Equal(1, SourceMath.IslandFactor(true, 2));
            Equal(1, SourceMath.IslandFactor(true, double.NaN));
            Equal(2000, SourceMath.AdjustedThreatWealth(10000, 2000, true, 0.5, 0.5));
            Equal(5000, SourceMath.AdjustedThreatWealth(10000, 2000, false, 1, 0.5));
            Equal(0, SourceMath.AdjustedThreatWealth(10000, 2000, true, 1, SourceMath.IslandFactor(true, 0)));
        });
        Check("island detection accepts enclosed land and excludes mainland and world edges", () =>
        {
            bool IsLandChainEnclosed(int landCount, bool atEdge)
            {
                return IslandLandmass.FindEnclosed(0, tile => tile == -1, tile => atEdge && tile == 1,
                    (tile, neighbors) =>
                    {
                        neighbors.Add(-1); // Water along the coast.
                        if (tile > 0) neighbors.Add(tile - 1);
                        if (tile + 1 < landCount) neighbors.Add(tile + 1);
                    })?.Count == landCount;
            }
            Require(IsLandChainEnclosed(1, false), "Single-tile island missed");
            Require(IsLandChainEnclosed(3, false), "Multi-tile island missed");
            Require(IsLandChainEnclosed(IslandLandmass.MaxWorldTiles, false), "Upper island limit should be inclusive");
            Require(!IsLandChainEnclosed(IslandLandmass.MaxWorldTiles + 1, false), "Mainland coastal tile treated as an island");
            Require(!IsLandChainEnclosed(3, true), "Partial-world boundary treated as water");
            Require(IslandLandmass.FindEnclosed(-1, tile => tile == -1, tile => false, (tile, neighbors) => { }) == null,
                "Water tile treated as an island");
        });
        Console.WriteLine("All " + passed + " checks passed.");
    }
}
