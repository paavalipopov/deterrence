using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public partial class SettingsEditor
    {
        private int labMode;
        private float labEnd;

        private void DrawCurves(Rect rect,DeterrenceSettings settings)
        {
            const float gap = 14;
            Rect controls;
            Rect preview;
            if (rect.width >= 660)
            {
                float width = Mathf.Clamp(rect.width * 0.45f, 300, 390);
                controls = new Rect(rect.x, rect.y, width, rect.height);
                preview = new Rect(controls.xMax + gap, rect.y, rect.width - width - gap, rect.height);
            }
            else
            {
                float height = rect.height * 0.57f;
                preview = new Rect(rect.x, rect.y, rect.width, height);
                controls = new Rect(rect.x, preview.yMax + gap, rect.width, Mathf.Max(0, rect.height - height - gap));
            }
            DrawCurveControls(controls, settings);
            DrawLaboratory(preview, settings);
        }

        private void DrawCurveControls(Rect rect, DeterrenceSettings settings)
        {
            Rect view=new Rect(0,0,rect.width-20,Math.Max(rect.height,curvesHeight));
            Widgets.BeginScrollView(rect,ref curvesScroll,view);
            var listing=HelpUI.SingleColumn(new Listing_Standard()); listing.Begin(view);
            DrawHazardGrowth(listing, settings);
            listing.GapLine();
            DrawFading(listing, settings);
            DrawExampleInputs(listing);
            curvesHeight=listing.CurHeight+20; listing.End(); Widgets.EndScrollView();
        }

        private void DrawHazardGrowth(Listing_Standard listing, DeterrenceSettings settings)
        {
            SectionTitle(listing, "Hazard growth");
            if (settings.GrowthShape == 0)
                HelpUI.Equation(listing, "GrowthLinearRate", "Daily gain  dH/dt = R",
                    "With p at zero, the gain stays at its starting rate throughout exposure.");
            else
                HelpUI.Equation(listing, "SharedGrowthRate", "Daily gain  dH/dt = R (1 + t / (pT))⁻ᵖ",
                    "For a steady hazard starting from zero. A change in rate carries the accumulated slowdown forward.");
            string mode = settings.GrowthShape == 0 ? "Linear" : settings.GrowthShape == 1 ? "Log" : "Power";
            string buildup = mode == "Linear" ? "H = Rt" : mode == "Log" ? "H grows like ln(t/T)" : "H grows like (t/T)^(1 − p)";
            HelpUI.Equation(listing, "HazardBuildup" + mode, "Long-run buildup  " + buildup,
                mode == "Linear" ? "At a steady rate R, buildup grows linearly from day one."
                    : "How buildup grows at a steady rate R when t is much larger than T. This trend omits constant factors. The graph shows the exact buildup at every day.");
            listing.Label("H is hazard buildup in points. R is the initial gain per day, t is days of exposure, and T is the slowdown time.");
            listing.Label("p sets the growth shape: 0 is linear, ½ is square root, 1 is logarithmic.");
            GlobalSlider(listing, "Growth shape", ref settings.GrowthShape, 0, 1, 0.01f, symbol: "p");

            listing.Gap(6);
            listing.Label("Slowdown time (T)");
            Rect timing = listing.GetRect(32);
            if (ChoiceButton(timing, 0, 2, "By total exposure", !settings.GrowthByRate)) settings.GrowthByRate = false;
            if (ChoiceButton(timing, 1, 2, "By daily rate", settings.GrowthByRate)) settings.GrowthByRate = true;
            if (settings.GrowthByRate)
            {
                HelpUI.Equation(listing, "RateTimescale", "T = D × √(R / 2,000)",
                    "D is the slowdown time at 2,000 points/day. Four times the rate gives twice the slowdown time.",
                    "D = days at 2,000/day");
                GlobalSlider(listing, "Slowdown days at 2,000/day", ref settings.SlowdownDays, 0.1f, 120, 0.1f, symbol: "D");
            }
            else
            {
                HelpUI.Equation(listing, "ExposureTimescale", "T = K / R",
                    "At twice the starting rate, it takes half as long to reach the exposure scale K.",
                    "K = raw exposure scale");
                GlobalSlider(listing, "Exposure scale", ref settings.SlowdownScale, 100, 1000000, 100, symbol: "K");
            }
            listing.Label("After 10 days at 2,000/day: " + F(settings.Growth.FromZero(2000, 10)) + " deterrence.");

            // Remains are a stock, so they always use K, even when hazards time their slowdown by rate.
            listing.Gap(6);
            listing.Label("Remains pass the total value of kept bodies through the same curve, with shape p and exposure scale K.");
            if (settings.GrowthByRate)
                GlobalSlider(listing, "Exposure scale", ref settings.SlowdownScale, 100, 1000000, 100, symbol: "K");
            var remains = settings.RemainsGrowth;
            listing.Label("Bodies worth 5,000, 20,000 and 60,000 count as " + F(remains.Stock(5000)) + ", "
                + F(remains.Stock(20000)) + " and " + F(remains.Stock(60000)) + ".");
        }

        private void DrawFading(Listing_Standard listing, DeterrenceSettings settings)
        {
            SectionTitle(listing, "Fading");
            HelpUI.Equation(listing, "FadeCubic", "Remaining = H₀ (1 − 3x² + 2x³)",
                "H₀ is the balance when fading begins. Both duration choices use this curve.",
                "H₀ = starting balance", largeNote: true);
            HelpUI.Equation(listing, "FadeProgress", "x = t / T",
                "The fraction of the fade elapsed runs from zero at the start to one at the end.",
                "x = fraction of fade elapsed", largeNote: true);
            listing.Label("Here t counts days since fading began, and T is the total fade duration. Half the balance remains at x = ½.");

            listing.Gap(6);
            listing.Label("Fade duration (T)");
            Rect basis = listing.GetRect(32);
            if (ChoiceButton(basis, 0, 2, "Starting balance", !settings.FadeByHistory)) settings.FadeByHistory = false;
            if (ChoiceButton(basis, 1, 2, "Hazard history", settings.FadeByHistory)) settings.FadeByHistory = true;
            string referenceDays = "D = " + F(settings.RecoveryDays) + " days at reference";
            if (settings.FadeByHistory)
            {
                HelpUI.Equation(listing, "HistoryDuration", "T = D (A / reference area)ᑫ",
                    "Compare the area under the active balance with the reference area.", referenceDays);
                HelpUI.Equation(listing, "HistoryArea", "A = ∫ H(t) dt",
                    "Add up the balance over the latest active episode to get point-days.",
                    "A = area during buildup");
                GlobalSlider(listing, "Reference area (point-days)", ref settings.FadeReferenceArea, 100, 1000000, 100);
                listing.Label("Reputation and removed bodies use their starting balance.");
            }
            else
            {
                HelpUI.Equation(listing, "FadeDuration", "T = D (H₀ / reference balance)ᑫ",
                    "Compare the starting balance with the reference balance.", referenceDays);
                GlobalSlider(listing, "Reference balance", ref settings.RecoveryReference, 100, 1000000, 100);
            }
            listing.Label("q sets how duration scales. At 0 it is fixed. At ½, four times the input lasts twice as long.");
            GlobalSlider(listing, "Duration exponent", ref settings.RecoveryExponent, 0, 1, 0.01f, symbol: "q");
        }

        private static bool ChoiceButton(Rect row,int index,int count,string label,bool selectedValue)
        {
            Rect button=new Rect(row.x+index*row.width/count,row.y,row.width/count-3,28);
            bool clicked=Widgets.ButtonText(button,label);
            if(selectedValue) Widgets.DrawHighlightSelected(button);
            return clicked;
        }

        private void DrawLaboratory(Rect rect, DeterrenceSettings settings)
        {
            var listing = HelpUI.SingleColumn(new Listing_Standard());
            listing.Begin(rect);
            HelpUI.Label(listing,"Example preview",
                "This graph starts from zero and uses the configured fallout and hive rates. Preview inputs affect the graph only.", true);
            Rect modes=listing.GetRect(30);
            string[] labels={"Hazards","Reputation","Combined"};
            for(int i=0;i<labels.Length;i++) if(ChoiceButton(modes,i,3,labels[i],labMode==i)) labMode=i;
            int colonists = ExampleColonists;
            string signature=string.Join("|",settings.GrowthByRate,settings.GrowthShape,settings.SlowdownDays,settings.SlowdownScale,settings.RecoveryDays,settings.RecoveryReference,
                settings.RecoveryExponent,settings.FadeByHistory,settings.FadeReferenceArea,labMode,colonists,labDays,labHiveStart,labHiveEnd,labBump,labInterval,labCount,
                settings.Rule("condition:ToxicFallout").AmountFor(colonists),settings.Rule("infestation:hives").AmountFor(colonists));
            if(labCurves==null || signature!=labSignature)
            {
                labSignature=signature;
                labCurves=DeterrenceGraphs.ScenarioView(settings,labMode,colonists,labDays,labHiveStart,labHiveEnd,
                    labBump,labInterval,labCount,out labEnd);
            }
            DeterrenceGraphs.Draw(listing.GetRect(Mathf.Clamp(rect.height - listing.CurHeight - 8, 90, 415)),
                labCurves,0,labEnd,"Example days");
            listing.End();
        }

        private void DrawExampleInputs(Listing_Standard listing)
        {
            listing.GapLine();
            if(Foldout(listing,"Preview inputs (examples only)",ref showLabInputs))
            {
                if(labMode!=1)
                {
                    if (showColonyControls)
                        previewPopulation=(int)PreviewSlider(listing,"Example colonists",previewPopulation,1,50,1);
                    labDays=PreviewSlider(listing,"Fallout duration (days)",labDays,1,30,1);
                    labHiveStart=PreviewSlider(listing,"First hive appears on day",labHiveStart,0,60,1);
                    labHiveEnd=PreviewSlider(listing,"All hives cleared on day",Math.Max(labHiveStart,labHiveEnd),labHiveStart,90,1);
                    listing.Label("One hive starts the cluster. At standard speed, each generation doubles after about 2½ days, longer as the cluster crowds, up to 30 hives.");
                }
                if(labMode!=0)
                {
                    labBump=PreviewSlider(listing,"Gain per act",labBump,0,20000,100);
                    labInterval=PreviewSlider(listing,"Days between acts",labInterval,1,30,1);
                    labCount=(int)PreviewSlider(listing,"Number of acts",labCount,1,8,1);
                }
            }
        }

        private void DrawCurvePreview(Listing_Standard listing,Source source,DeterrenceSettings settings)
        {
            HelpUI.Label(listing,"Preview starts from zero and uses the current source switches.",
                "Existing balances are not shown. The example is hypothetical: it creates no events and does not alter a running fade.");
            if (showColonyControls)
            {
                listing.Label("Example colonists");
                string buffer=Buffer("preview:population",previewPopulation.ToString());
                Widgets.TextFieldNumeric(listing.GetRect(30),ref previewPopulation,ref buffer,1,1000);
                buffers["preview:population"]=buffer;
            }
            int colonists = ExampleColonists;
            bool body=source.Rule.Key.StartsWith("remains:",StringComparison.Ordinal);
            bool act=RuleGrouping.IsBump(source.Rule.Key);
            if(!act) previewDays=PreviewSlider(listing,body?"Days body is kept":"Days source is active",previewDays,1,120,1);
            if(IsPerPower(source.Rule.Key))
            {
                previewPower=PreviewSlider(listing,"Example combat power",previewPower,1,1000,1);
                if(source.Rule.Key=="bump:human" && settings.IncludeHumanGear)
                    previewGear=PreviewSlider(listing,"Original gear value",previewGear,0,20000,25);
            }
            bool fraction=source.Rule.Key=="pollution:ground";
            bool heat=source.Rule.Key=="climate:heat",cold=source.Rule.Key=="climate:cold";
            bool units=fraction || heat || cold || source.Rule.Key=="infestation:hives";
            if(fraction)
            {
                listing.Label("Polluted ground: "+Math.Min(previewExposure,1).ToString("P0"));
                previewExposure=Mathf.Round(listing.Slider(Math.Min(previewExposure,1),0,1)*100)/100;
            }
            else if(heat||cold)
            {
                float delta=PreviewSlider(listing,heat?"Degrees above heat threshold":"Degrees below cold threshold",previewExposure*10,0,100,1);
                previewExposure=delta/10;
                listing.Label("Example outdoor temperature: "+F(heat?settings.HeatThreshold+delta:settings.ColdThreshold-delta)+" C");
            }
            else if(units) previewExposure=PreviewSlider(listing,"Hostile hive count",previewExposure,0,10,1);
            double exposure=units?previewExposure:1;
            string signature=string.Join("|",source.Rule.Key,source.Rule.Enabled,source.Rule.BaseAmount,source.Installed,CategoryEnabled(source,settings),
                source.Rule.PopulationExponent,source.Rule.ReferencePopulation,colonists,previewDays,previewPower,previewGear,exposure,
                settings.GrowthByRate,settings.GrowthShape,settings.SlowdownDays,settings.SlowdownScale,settings.RecoveryDays,settings.RecoveryReference,settings.RecoveryExponent,settings.FadeByHistory,settings.FadeReferenceArea,
                settings.IncludeHumanGear,settings.HumanGearFraction);
            if(previewCurves==null || signature!=previewSignature)
            {
                previewSignature=signature;
                previewCurves=DeterrenceGraphs.Preview(PreviewRule(source,settings),settings,colonists,previewDays,exposure,body,previewPower,previewGear);
                previewEnd=previewCurves.Max(c=>c.curve.Points.Last().x);
            }
            DeterrenceGraphs.Draw(listing.GetRect(280),previewCurves,0,previewEnd,"Example days");
        }
    }
}
