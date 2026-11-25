using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Antiwealth
{
    public partial class SettingsEditor
    {
        private int labMode;
        private float labEnd;

        private void DrawCurves(Rect rect,AntiwealthSettings settings)
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

        private void DrawCurveControls(Rect rect, AntiwealthSettings settings)
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

        private void DrawHazardGrowth(Listing_Standard listing, AntiwealthSettings settings)
        {
            SectionTitle(listing, "Hazard growth");
            listing.Label("R is the starting gain in points per day. T sets how soon that gain slows.");
            if (settings.GrowthByRate)
                HelpUI.Equation(listing, "RateTimescale", "T = D × √(R / 2,000)",
                    "D is the slowdown time at 2,000 points/day. Four times the rate gives twice the slowdown time.",
                    "D = days at 2,000/day");
            else
                HelpUI.Equation(listing, "ExposureTimescale", "T = K / R",
                    "K is the raw exposure scale in points. At twice the starting rate, it takes half as long to reach K.",
                    "K = raw exposure scale");

            Rect timing = listing.GetRect(32);
            if (ChoiceButton(timing, 0, 2, "By total exposure", !settings.GrowthByRate)) settings.GrowthByRate = false;
            if (ChoiceButton(timing, 1, 2, "By daily rate", settings.GrowthByRate)) settings.GrowthByRate = true;
            if (settings.GrowthByRate)
            {
                listing.Label("At higher starting rates, slowdown comes later.");
                GlobalSlider(listing, "Slowdown days at 2,000/day", ref settings.SlowdownDays, 0.1f, 120, 0.1f, symbol: "D");
            }
            else
            {
                listing.Label("Stronger hazards reach the same exposure sooner.");
                GlobalSlider(listing, "Exposure scale", ref settings.SlowdownScale, 100, 1000000, 100, symbol: "K");
            }

            listing.Gap(6);
            listing.Label("H is hazard deterrence. At a steady rate, t counts active days.");
            if (settings.GrowthShape == 0)
                HelpUI.Equation(listing, "GrowthLinearRate", "Daily gain  dH/dt = R",
                    "With p at zero, the balance grows by R points every active day.");
            else
                HelpUI.Equation(listing, "SharedGrowthRate", "Daily gain  dH/dt = R (1 + t / (pT))⁻ᵖ",
                    "For a steady hazard starting from zero. If its rate changes, the game carries its slowdown forward.");
            listing.Label("p shapes the buildup. At 0 it is linear, at ½ square root, and at 1 logarithmic.");
            GlobalSlider(listing, "Growth shape", ref settings.GrowthShape, 0, 1, 0.01f, symbol: "p");
            listing.Label("After 10 days at 2,000/day: " + F(settings.Growth.FromZero(2000, 10)) + " deterrence.");
        }

        private void DrawFading(Listing_Standard listing, AntiwealthSettings settings)
        {
            SectionTitle(listing, "Fading");
            listing.Label("F is the time until the balance reaches zero. At the reference value it lasts L = "
                + F(settings.RecoveryDays) + " days.");
            if (settings.FadeByHistory)
            {
                HelpUI.Equation(listing, "HistoryArea", "A = ∫ H(t) dt",
                    "Add up the hazard balance over its latest active episode, in point-days.",
                    "A = buildup in point-days");
                HelpUI.Equation(listing, "HistoryDuration", "F = L (A / reference area)ᑫ",
                    "Compare the episode's area A with the reference area. At the reference area the fade lasts L days.");
            }
            else
                HelpUI.Equation(listing, "FadeDuration", "F = L (B₀ / reference balance)ᑫ",
                    "Compare the balance at the start of fading with the reference balance.",
                    "B₀ = starting balance");

            Rect basis = listing.GetRect(32);
            if (ChoiceButton(basis, 0, 2, "Starting balance", !settings.FadeByHistory)) settings.FadeByHistory = false;
            if (ChoiceButton(basis, 1, 2, "Hazard history", settings.FadeByHistory)) settings.FadeByHistory = true;
            if (settings.FadeByHistory)
            {
                listing.Label("A longer-lived hazard can linger longer. Reputation and removed bodies use starting balance.");
                GlobalSlider(listing, "Reference area (point-days)", ref settings.FadeReferenceArea, 100, 1000000, 100);
            }
            else GlobalSlider(listing, "Reference balance", ref settings.RecoveryReference, 100, 1000000, 100);
            listing.Label("q sets how duration scales. At 0 it is fixed. At ½, four times the input lasts twice as long.");
            GlobalSlider(listing, "Duration exponent", ref settings.RecoveryExponent, 0, 1, 0.01f, symbol: "q");

            listing.Gap(6);
            HelpUI.Equation(listing, "FadeProgress", "u = elapsed days / F",
                "Count days from the moment fading starts. At u = 1 the balance is gone.",
                "u = fraction of fade elapsed");
            HelpUI.Equation(listing, "FadeCubic", "Remaining = B₀ (1 − 3u² + 2u³)",
                "B₀ is the balance when fading starts. The same curve applies to both duration choices.",
                settings.FadeByHistory ? "B₀ = starting balance" : null);
            listing.Label("Half the starting balance remains halfway through the fade.");
        }

        private static bool ChoiceButton(Rect row,int index,int count,string label,bool selectedValue)
        {
            Rect button=new Rect(row.x+index*row.width/count,row.y,row.width/count-3,28);
            bool clicked=Widgets.ButtonText(button,label);
            if(selectedValue) Widgets.DrawHighlightSelected(button);
            return clicked;
        }

        private void DrawLaboratory(Rect rect, AntiwealthSettings settings)
        {
            var listing = HelpUI.SingleColumn(new Listing_Standard());
            listing.Begin(rect);
            HelpUI.Label(listing,"Example preview",
                "This graph starts from zero and uses the configured fallout and hive rates. Preview inputs affect the graph only.", true);
            Rect modes=listing.GetRect(30);
            string[] labels={"Hazard overlap","Repeated acts","Combined"};
            for(int i=0;i<labels.Length;i++) if(ChoiceButton(modes,i,3,labels[i],labMode==i)) labMode=i;
            int colonists = ExampleColonists;
            string signature=string.Join("|",settings.GrowthByRate,settings.GrowthShape,settings.SlowdownDays,settings.SlowdownScale,settings.RecoveryDays,settings.RecoveryReference,
                settings.RecoveryExponent,settings.FadeByHistory,settings.FadeReferenceArea,labMode,colonists,labDays,labBump,labInterval,labCount,
                settings.Rule("condition:ToxicFallout").AmountFor(colonists),settings.Rule("infestation:hives").AmountFor(colonists));
            if(labCurves==null || signature!=labSignature)
            {
                labSignature=signature;
                labCurves=AntiwealthGraphs.LaboratoryView(settings,labMode,colonists,labDays,labBump,labInterval,labCount,out labEnd);
            }
            AntiwealthGraphs.Draw(listing.GetRect(Mathf.Clamp(rect.height - listing.CurHeight - 8, 90, 415)),
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
                }
                if(labMode!=0)
                {
                    labBump=PreviewSlider(listing,"Gain per act",labBump,0,20000,100);
                    labInterval=PreviewSlider(listing,"Days between acts",labInterval,1,30,1);
                    labCount=(int)PreviewSlider(listing,"Number of acts",labCount,1,8,1);
                }
            }
        }

        private void DrawCurvePreview(Listing_Standard listing,Source source,AntiwealthSettings settings)
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
                previewCurves=AntiwealthGraphs.Preview(PreviewRule(source,settings),settings,colonists,previewDays,exposure,body,previewPower,previewGear);
                previewEnd=previewCurves.Max(c=>c.curve.Points.Last().x);
            }
            AntiwealthGraphs.Draw(listing.GetRect(280),previewCurves,0,previewEnd,"Example days");
        }
    }
}
