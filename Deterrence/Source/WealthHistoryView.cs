using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public class WealthHistoryView
    {
        private bool economy = true, vanilla = true, adjusted = true, credit = true;
        private int revision = -1, mapId = -1;
        private DeterrenceGameComponent currentLedger;
        private List<WealthSample> samples = new List<WealthSample>();
        private List<SimpleCurveDrawInfo> curves = new List<SimpleCurveDrawInfo>();

        public void Draw(Rect rect, Map map, DeterrenceGameComponent ledger)
        {
            if (!map.IsPlayerHome || map.IsPocketMap)
            {
                Widgets.Label(rect, "Wealth history is recorded for colony home maps only.");
                return;
            }
            if (currentLedger != ledger || revision != ledger.HistoryRevision || mapId != map.uniqueID)
            {
                samples = ledger.HistoryFor(map).OrderBy(s => s.Day).ToList();
                currentLedger = ledger; mapId = map.uniqueID; revision = ledger.HistoryRevision; BuildCurves();
            }
            var heading = new Rect(rect.x, rect.y, rect.width, 44);
            Widgets.Label(heading,
                "Wealth and threat over time. Hover a curve for values.");
            TooltipHandler.TipRegion(heading,
                "Actual wealth counts all assets. Base threat counts buildings at half value and includes Options adjustments. Threat also includes deterrence and terrain factors. Old samples retain their recorded values.");
            bool e = economy, v = vanilla, a = adjusted, c = credit;
            float half = rect.width / 2;
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y + 49, half - 8, 28), "Actual wealth", ref economy);
            Widgets.CheckboxLabeled(new Rect(rect.x + half, rect.y + 49, half - 8, 28), "Base threat", ref vanilla);
            Widgets.CheckboxLabeled(new Rect(rect.x, rect.y + 79, half - 8, 28), "Threat", ref adjusted);
            Widgets.CheckboxLabeled(new Rect(rect.x + half, rect.y + 79, half - 8, 28), "Deterrence", ref credit);
            if (e != economy || v != vanilla || a != adjusted || c != credit) BuildCurves();
            Rect plot = new Rect(rect.x, rect.y + 122, rect.width, rect.height - 158);
            if (samples.Count < 2)
                Widgets.Label(plot, "Let half a game day pass to see a curve.");
            else DeterrenceGraphs.Draw(plot, curves, samples[0].Day, samples[samples.Count - 1].Day, "Days since game start");
            if (samples.Count > 0) Widgets.Label(new Rect(rect.x, rect.yMax - 30, rect.width, 28),
                "Recorded from day " + samples[0].Day.ToString("0.0") + ".");
        }

        private void BuildCurves()
        {
            curves = new List<SimpleCurveDrawInfo>();
            if (economy) curves.Add(DeterrenceGraphs.Series("Actual wealth", DeterrenceGraphs.EconomyColor, samples.Select(s => new CurvePoint(s.Day, s.Economy))));
            if (vanilla) curves.Add(DeterrenceGraphs.Series("Base threat", DeterrenceGraphs.VanillaColor, samples.Select(s => new CurvePoint(s.Day, (float)CreditMath.EffectiveWealth(s.VanillaThreat, s.SurfaceDiscount)))));
            if (adjusted) curves.Add(DeterrenceGraphs.Series("Threat", DeterrenceGraphs.AdjustedColor, samples.Select(s => new CurvePoint(s.Day, s.AdjustedThreat))));
            if (credit) curves.Add(DeterrenceGraphs.Series("Deterrence", DeterrenceGraphs.CreditColor, samples.Select(s => new CurvePoint(s.Day, s.Deterrence))));
        }
    }
}
