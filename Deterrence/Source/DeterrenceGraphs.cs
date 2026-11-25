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

        public static List<SimpleCurveDrawInfo> LaboratoryView(DeterrenceSettings settings,int mode,int population,
            float activeDays,float gain,float interval,int count,out float end)
        {
            var all=Laboratory(settings,population,activeDays,mode==0?0:gain,interval,count);
            end=all.Max(c=>c.curve.Points.Last().x);
            if(mode==0)
            {
                all[3].label="Total hazards";
                return new List<SimpleCurveDrawInfo>{all[0],all[1],all[3]};
            }
            if(mode==1)
            {
                double last=(count-1)*interval;
                double peak=PreviewMath.Bumps(last,gain,interval,count,settings.RecoveryDays,settings.RecoveryReference,settings.RecoveryExponent,FadeCurve.Cubic);
                end=(float)(last+CreditMath.Duration(peak,settings.RecoveryDays,settings.RecoveryReference,settings.RecoveryExponent)+1);
                double duration=CreditMath.Duration(gain,settings.RecoveryDays,settings.RecoveryReference,settings.RecoveryExponent);
                return new List<SimpleCurveDrawInfo>{Series("One act only",VanillaColor,
                    all[2].curve.Points.Select(p=>new CurvePoint(p.x,(float)CreditMath.Remaining(gain,p.x,duration,FadeCurve.Cubic)))),all[2]};
            }
            return all;
        }

        public static List<SimpleCurveDrawInfo> Laboratory(DeterrenceSettings settings, int population,
            float activeDays, float bump, float interval, int count)
        {
            double aRate = settings.Rule("condition:ToxicFallout", 4000).AmountFor(population);
            double bRate = settings.Rule("infestation:hives", 2000).AmountFor(population);
            var fallout = new HazardExample(settings, aRate, activeDays);
            var hive = new HazardExample(settings, bRate, activeDays + 2);
            double lastBump = (count - 1) * interval;
            double bumpPeak = PreviewMath.Bumps(lastBump, bump, interval, count, settings.RecoveryDays,
                settings.RecoveryReference, settings.RecoveryExponent, FadeCurve.Cubic);
            double end = Math.Max(activeDays + fallout.FadeDays,
                Math.Max(activeDays + 5 + hive.FadeDays,
                    lastBump + CreditMath.Duration(bumpPeak, settings.RecoveryDays, settings.RecoveryReference, settings.RecoveryExponent))) + 1;
            var times = new SortedSet<double>(Enumerable.Range(0, 301).Select(i => end * i / 300));
            times.Add(activeDays); times.Add(3); times.Add(activeDays + 5);
            for (int i = 0; i < count; i++)
            {
                double t = i * interval;
                if (t > 0) times.Add(t - 0.0001);
                times.Add(t);
            }
            var first = new List<CurvePoint>(); var second = new List<CurvePoint>();
            var violence = new List<CurvePoint>(); var total = new List<CurvePoint>();
            foreach (double t in times)
            {
                double a = fallout.At(t);
                double b = hive.At(t - 3);
                double v = PreviewMath.Bumps(t, bump, interval, count, settings.RecoveryDays,
                    settings.RecoveryReference, settings.RecoveryExponent, FadeCurve.Cubic);
                first.Add(new CurvePoint((float)t, (float)a)); second.Add(new CurvePoint((float)t, (float)b));
                violence.Add(new CurvePoint((float)t, (float)v)); total.Add(new CurvePoint((float)t, (float)(a+b+v)));
            }
            return new List<SimpleCurveDrawInfo> { Series("Fallout", CreditColor, first), Series("One hive", VanillaColor, second),
                Series("Reputation", new Color(1f,0.45f,0.5f), violence), Series("Total", EconomyColor, total) };
        }
    }
}
