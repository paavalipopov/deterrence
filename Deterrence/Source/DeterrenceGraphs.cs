using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public static class DeterrenceGraphs
    {
        public static readonly Color CreditColor = new Color(0.3f, 0.85f, 0.8f);
        public static readonly Color AdjustedColor = new Color(0.6f, 0.85f, 1f);
        public static readonly Color VanillaColor = new Color(1f, 0.7f, 0.3f);
        public static readonly Color EconomyColor = new Color(1f, 1f, 0.3f);

        public static SimpleCurveDrawInfo Series(string label, Color color, IEnumerable<CurvePoint> points)
        {
            var curve = new SimpleCurve(); curve.SetPoints(points);
            return new SimpleCurveDrawInfo { label = label, color = color, curve = curve, valueFormat = "{0:0}" };
        }

        public static void Draw(Rect rect, List<SimpleCurveDrawInfo> curves, float start, float end, string axisLabel, float? fixedMaximum = null)
        {
            if (curves.Count == 0) { Widgets.Label(rect, "Choose at least one curve."); return; }
            float max = curves.SelectMany(c => c.curve.Points).Select(p => p.y).DefaultIfEmpty(1).Max();
            var style = new SimpleCurveDrawerStyle
            {
                DrawBackground = true, DrawBackgroundLines = true, DrawMeasures = true,
                DrawPoints = false, DrawLegend = true, DrawCurveMousePoint = true,
                OnlyPositiveValues = true, UseFixedSection = true, FixedSection = new FloatRange(start, Math.Max(start + 0.01f, end)),
                UseFixedScale = true, FixedScale = new Vector2(0, fixedMaximum ?? Math.Max(1, max * 1.08f)),
                MeasureLabelsXCount = rect.width < 420 ? 3 : 5, MeasureLabelsYCount = 4,
                LabelX = axisLabel, XIntegersOnly = false, YIntegersOnly = false,
                UseAntiAliasedLines = true
            };
            Rect graph = new Rect(rect.x + 6, rect.y + 4, rect.width - 12, Math.Max(40, rect.height - 61));
            Rect legend = new Rect(rect.x, rect.yMax - 48, rect.width, 46);
            SimpleCurveDrawer.DrawCurves(graph, curves, style, null, legend);
        }

        private readonly struct HazardExample
        {
            private readonly HazardGrowth growth;
            private readonly double rate, activeDays;
            public readonly double Peak, FadeDays;

            public HazardExample(DeterrenceSettings settings, double rate, double activeDays)
            {
                growth = settings.Growth;
                this.rate = rate;
                this.activeDays = activeDays;
                Peak = growth.Earn(rate, activeDays, 0, 0, out double area);
                FadeDays = settings.FadeDuration(Peak, area, true);
            }

            public double At(double day)
            {
                if (day <= 0) return 0;
                if (day < activeDays) return growth.FromZero(rate, day);
                return CreditMath.Remaining(Peak, day - activeDays, FadeDays);
            }
        }

        public static List<SimpleCurveDrawInfo> Preview(RuleValues rule, DeterrenceSettings settings,
            int population, float activeDays, double unitExposure, bool remains, double samplePower, double sampleGear)
        {
            double rate = rule.AmountFor(population) * unitExposure;
            bool bump = RuleGrouping.IsBump(rule.Key);
            bool death = RuleGrouping.IsDeathBump(rule.Key);
            var hazard = new HazardExample(settings, rate, activeDays);
            double peak = remains ? SourceMath.MaintainedRemains(samplePower, rule, population)
                : death ? SourceMath.DeathBump(samplePower, rule, population,
                    rule.Key == "bump:human" && settings.IncludeHumanGear ? sampleGear : 0, settings.HumanGearFraction)
                : bump ? rule.AmountFor(population)
                : hazard.Peak;
            double duration = bump || remains ? settings.FadeDuration(peak, 0, false) : hazard.FadeDays;
            var growth = new List<CurvePoint>();
            if (bump) { activeDays = 0; growth.Add(new CurvePoint(0, 0)); growth.Add(new CurvePoint(0, (float)peak)); }
            else for (int i = 0; i <= 100; i++)
            {
                double t = activeDays * i / 100.0;
                growth.Add(new CurvePoint((float)t, (float)(remains ? peak : hazard.At(t))));
            }
            var recovery = new List<CurvePoint>();
            for (int i = 0; i <= 100; i++)
            {
                double t = duration * i / 100.0;
                recovery.Add(new CurvePoint((float)(activeDays + t), (float)CreditMath.Remaining(peak, t, duration, FadeCurve.Cubic)));
            }
            return new List<SimpleCurveDrawInfo>
            {
                Series(bump ? "Immediate gain" : remains ? "Body present" : "Hazard buildup", CreditColor, growth),
                Series("Fading", AdjustedColor, recovery)
            };
        }

        // Illustrative generations at standard speed with room to spread. Nearby hives
        // all share one room. Real reproduction rolls a separate interval for each hive.
        private sealed class HiveExample
        {
            public readonly List<double> BirthDays = new List<double>();
            public readonly double FadeDays;
            private readonly double start, stop, unitRate, peak;
            private readonly HazardGrowth growth;

            public HiveExample(DeterrenceSettings settings, double unitRate, double start, double stop)
            {
                this.start = start;
                this.stop = Math.Max(start, stop);
                this.unitRate = unitRate;
                growth = settings.Growth;
                double day = start;
                for (int n = 1; n < 30; n *= 2)
                {
                    // CompProperties_SpawnerHives defaults: 2–3 days; density factor
                    // falls from 1 at zero nearby hives to 0.35 at seven (including self).
                    double factor = 1 - 0.65 * Math.Min(n, 7) / 7;
                    day += 2.5 / factor;
                    if (day >= this.stop) break;
                    BirthDays.Add(day);
                }
                var final = BuildTo(this.stop);
                peak = final.Balance;
                FadeDays = peak > 0 ? settings.FadeDuration(peak, final.ActiveArea, true) : 0;
            }

            private CreditState BuildTo(double day)
            {
                var state = new CreditState();
                double last = start;
                int count = 1;
                foreach (double birth in BirthDays)
                {
                    if (birth >= day) break;
                    state.Accrue(unitRate * count, birth - last, growth);
                    count = Math.Min(30, count * 2);
                    last = birth;
                }
                state.Accrue(unitRate * count, day - last, growth);
                return state;
            }

            public double At(double day) => day <= start ? 0 : day < stop ? BuildTo(day).Balance
                : CreditMath.Remaining(peak, day - stop, FadeDays);
        }

        public static List<SimpleCurveDrawInfo> LaboratoryView(DeterrenceSettings settings,int mode,int population,
            float activeDays,float gain,float interval,int count,out float end)
            => ScenarioView(settings, mode, population, activeDays, 3, activeDays + 5, gain, interval, count, out end);

        public static List<SimpleCurveDrawInfo> Laboratory(DeterrenceSettings settings, int population,
            float activeDays, float bump, float interval, int count)
            => LaboratoryView(settings, 2, population, activeDays, bump, interval, count, out _);

        public static List<SimpleCurveDrawInfo> ScenarioView(DeterrenceSettings settings, int mode, int population,
            float activeDays, float hiveStart, float hiveEnd, float bump, float interval, int count, out float end)
        {
            double aRate = settings.Rule("condition:ToxicFallout", 4000).AmountFor(population);
            double bRate = settings.Rule("infestation:hives", 2000).AmountFor(population);
            var fallout = new HazardExample(settings, aRate, activeDays);
            var hive = new HiveExample(settings, bRate, hiveStart, hiveEnd);
            double lastBump = (count - 1) * interval;
            double bumpPeak = PreviewMath.Bumps(lastBump, bump, interval, count, settings.RecoveryDays,
                settings.RecoveryReference, settings.RecoveryExponent, FadeCurve.Cubic);
            double hazardEnd = Math.Max(activeDays + fallout.FadeDays, hiveEnd + hive.FadeDays);
            double reputationEnd = lastBump + settings.FadeDuration(bumpPeak, 0, false);
            double horizon = (mode == 0 ? hazardEnd : mode == 1 ? reputationEnd : Math.Max(hazardEnd, reputationEnd)) + 1;
            end = (float)horizon;
            var times = new SortedSet<double>(Enumerable.Range(0, 301).Select(i => horizon * i / 300));
            if (mode != 1)
            {
                times.UnionWith(hive.BirthDays);
                times.UnionWith(new double[] { activeDays, activeDays + fallout.FadeDays, hiveStart, hiveEnd, hiveEnd + hive.FadeDays });
            }
            for (int i = 0; mode != 0 && i < count; i++)
            {
                double t = i * interval;
                if (t > 0) times.Add(t - 0.0001);
                times.Add(t);
            }
            var first = new List<CurvePoint>(); var second = new List<CurvePoint>();
            var violence = new List<CurvePoint>(); var total = new List<CurvePoint>();
            foreach (double t in times)
            {
                double a = mode == 1 ? 0 : fallout.At(t);
                double b = mode == 1 ? 0 : hive.At(t);
                double v = mode == 0 ? 0 : PreviewMath.Bumps(t, bump, interval, count, settings.RecoveryDays,
                    settings.RecoveryReference, settings.RecoveryExponent, FadeCurve.Cubic);
                first.Add(new CurvePoint((float)t, (float)a)); second.Add(new CurvePoint((float)t, (float)b));
                violence.Add(new CurvePoint((float)t, (float)v)); total.Add(new CurvePoint((float)t, (float)(a+b+v)));
            }
            var curves = new List<SimpleCurveDrawInfo>();
            if (mode != 1)
            {
                curves.Add(Series("Fallout", CreditColor, first));
                curves.Add(Series("Hives", VanillaColor, second));
            }
            if (mode == 1)
            {
                double fade = settings.FadeDuration(bump, 0, false);
                curves.Add(Series("One act only", VanillaColor, times.Select(t =>
                    new CurvePoint((float)t, (float)CreditMath.Remaining(bump, t, fade)))));
            }
            if (mode != 0) curves.Add(Series("Reputation", new Color(1f,0.45f,0.5f), violence));
            if (mode != 1) curves.Add(Series(mode == 0 ? "Total hazards" : "Total", EconomyColor, total));
            return curves;
        }
    }
}
