using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using Verse;

namespace Antiwealth
{
    public partial class SettingsEditor
    {
        private void DrawSources(Rect rect, AntiwealthSettings settings)
        {
            var header = HelpUI.SingleColumn(new Listing_Standard()); header.Begin(rect);
            if (tab == (int)SettingsSection.Hazards)
            {
                HelpUI.Checkbox(header, "Enable hazards", ref settings.Conditions,
                    "Controls accumulation from hazards. Turning it off starts fading current hazard balances on refresh.", true);
                header.Label("Each hazard type builds its own balance while present, then fades after it ends.");
            }
            else if (tab == (int)SettingsSection.Remains)
            {
                HelpUI.Checkbox(header, "Enable bodies", ref settings.Corpses,
                    "Controls deterrence from hostile bodies, including burials. Turning it off starts each body's fade on refresh.", true);
                var organs = settings.Rule("organs:local",0.75f);
                HelpUI.Checkbox(header, "Enable organ stock", ref organs.Enabled,
                    "Counts organs harvested locally from prisoners while the stock is kept.", true);
                header.Label("Bodies fade when removed. Kept organ value ends when the stock leaves.");
            }
            else
            {
                HelpUI.Checkbox(header, "Enable reputation", ref settings.Reputation,
                    "Hostile deaths, certain prisoner surgeries, executions, and the optional ritual reward can add reputation. The current balance keeps fading.", true);
                header.Label("These events share one balance. Each positive gain adds to what remains and renews its fade.");
            }
            float headerHeight = header.CurHeight + 12; header.End();
            rect = new Rect(rect.x, rect.y + headerHeight, rect.width, Math.Max(80, rect.height - headerHeight));
            float leftWidth = Mathf.Clamp(rect.width * 0.32f, 175, 240);
            Rect left = new Rect(rect.x,rect.y,leftWidth,rect.height);
            Rect right = new Rect(left.xMax + 15,rect.y,rect.width-leftWidth-15,rect.height);
            Widgets.Label(new Rect(left.x,left.y,left.width,24),"Search sources");
            search = Widgets.TextField(new Rect(left.x,left.y+25,left.width,28),search);
            float listTop = 62;
            if (tab == (int)SettingsSection.Hazards)
            {
                Widgets.CheckboxLabeled(new Rect(left.x,left.y+60,left.width,28),"Show other conditions",ref showOtherConditions);
                TooltipHandler.TipRegion(new Rect(left.x,left.y+60,left.width,28),
                    "Show other discovered game/mod conditions. Main sources stay listed even when disabled. Searching also includes additional conditions.");
                listTop = 97;
            }
            var sources = Sources(settings).Where(s => (int)RuleGrouping.SectionFor(s.Rule.Key) == tab
                && SourceVisible(s,showOtherConditions,search,selected)).ToList();
            if (!sources.Any(s => s.Rule.Key == selected)) SelectSource(sources.FirstOrDefault()?.Rule.Key);
            float totalHeight = sources.Count*54 + sources.Select(s=>s.Group).Distinct().Count()*25;
            Rect outer = new Rect(left.x,left.y+listTop,left.width,Math.Max(40,left.height-listTop));
            Rect view = new Rect(0,0,left.width-18,Math.Max(outer.height,totalHeight+4));
            Widgets.BeginScrollView(outer,ref listScroll,view);
            float y=0; string group=null;
            foreach (var source in sources)
            {
                if (source.Group != group)
                {
                    group=source.Group; Text.Font=GameFont.Tiny;
                    Widgets.Label(new Rect(4,y,view.width-8,25),group); Text.Font=GameFont.Small; y+=25;
                }
                Rect row = new Rect(0,y,view.width,52); y+=54;
                if (source.Rule.Key == selected) Widgets.DrawHighlightSelected(row);
                else if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                string state=SourceState(source,settings);
                string amount=source.Rule.BaseAmount.ToString("0.##") + (source.Rule.Key=="organs:local" ? "x value"
                    : IsPerPower(source.Rule.Key) ? " per power" : RuleGrouping.IsBump(source.Rule.Key) ? " per act" : "/day");
                Widgets.Label(row.ContractedBy(4),source.Name.Truncate(row.width-8)+"\n"+(state=="On" ? amount : state));
                TooltipHandler.TipRegion(row,source.Name+"\n"+source.Unit+"\n"+state);
                if (Widgets.ButtonInvisible(row)) SelectSource(source.Rule.Key);
            }
            Widgets.EndScrollView();
            var chosen=sources.FirstOrDefault(s=>s.Rule.Key==selected);
            if(chosen==null) { Widgets.Label(right,"No matching sources."); return; }
            DrawDetails(right,chosen,settings);
        }

        private void DrawDetails(Rect rect, Source source, AntiwealthSettings settings)
        {
            if(source.Rule.Key=="organs:local") { DrawOrganDetails(rect,source,settings); return; }
            var rule=source.Rule;
            Rect view=new Rect(0,0,rect.width-20,Math.Max(rect.height,detailHeight));
            Widgets.BeginScrollView(rect,ref detailScroll,view);
            var listing=HelpUI.SingleColumn(new Listing_Standard()); listing.Begin(view);
            SectionTitle(listing,source.Name);
            bool body=rule.Key.StartsWith("remains:",StringComparison.Ordinal);
            bool act=RuleGrouping.IsBump(rule.Key);
            listing.Label(body ? "Counts while kept or buried. Fades after removal."
                : act ? "Adds to Reputation and restarts its fade."
                : "Builds while active and fades when it ends.");
            string state=SourceState(source,settings);
            if(state!="On") listing.Label("Status: "+state+". You can still configure its amount.");
            HelpUI.Checkbox(listing,"Enable source",ref rule.Enabled,
                act ? "Turning this off stops new gains from this act. Existing shared Reputation keeps fading."
                : "Turning this off ends maintained/growing contributions on refresh and starts their fade.");
            if(rule.Key=="climate:heat") GlobalSlider(listing,"Heat threshold (C)",ref settings.HeatThreshold,Math.Max(-100,settings.ColdThreshold),150,1);
            if(rule.Key=="climate:cold") GlobalSlider(listing,"Cold threshold (C)",ref settings.ColdThreshold,-150,Math.Min(100,settings.HeatThreshold),1);
            DrawSourceEquation(listing, rule.Key, settings);
            HelpUI.Label(listing,(IsPerPower(rule.Key) ? "Weight per combat power" : act ? "Deterrence per act" : "Initial daily rate")
                +": "+rule.BaseAmount.ToString("0.##"),source.Unit+". "+(IsPerPower(rule.Key)
                ? "Combat power is a fixed pawn-kind rating; injuries and skills do not change it."
                : act ? "Adds this amount once per qualifying act, before colony scaling." : EnvironmentHelp(rule.Key)));
            AmountInput(listing,rule.Key+":amount",ref rule.BaseAmount,0,1000000,IsPerPower(rule.Key)?0.1f:25,IsPerPower(rule.Key)?10:20000);
            if(rule.Key=="remains:animal") DrawAnimalPresets(listing,rule);
            if(rule.Key=="bump:human")
            {
                HelpUI.Checkbox(listing,"Include original gear",ref settings.IncludeHumanGear,
                    "Adds weapons and worn apparel to the death gain. Captured before downing or direct death. This does not change maintained body value.");
                if(settings.IncludeHumanGear) GlobalSlider(listing,"Gear value share",ref settings.HumanGearFraction,0,1,0.05f,true);
            }
            DrawStandardExample(listing,source,settings);
            if(showColonyControls && Foldout(listing,"Colony scaling: "+PopulationName(rule),ref showPopulation))
                DrawPopulation(listing,rule);
            if(Foldout(listing,"Preview",ref showPreview)) DrawCurvePreview(listing,source,settings);
            string resetLabel = inheritedSettings == null ? "Reset amount" : "Use Global amount";
            if (showColonyControls) resetLabel += " and colony scaling";
            if(listing.ButtonText(resetLabel))
            {
                var inherited = inheritedSettings?.Rule(rule.Key);
                rule.BaseAmount = inherited?.BaseAmount ?? settings.LegacyDefault(rule.Key);
                if (showColonyControls)
                {
                    rule.PopulationExponent = inherited?.PopulationExponent ?? 0;
                    rule.ReferencePopulation = inherited?.ReferencePopulation ?? 5;
                    buffers.Remove(rule.Key+":reference");
                    buffers.Remove(rule.Key+":exponent");
                }
                ClearFactionRuleEdits(rule.Key, false, showColonyControls);
                buffers.Remove(rule.Key+":amount");
            }
            detailHeight=listing.CurHeight+20; listing.End(); Widgets.EndScrollView();
        }

        private void DrawAnimalPresets(Listing_Standard listing,EventRule rule)
        {
            Rect row=listing.GetRect(30);
            string[] labels={"Off (0)","Default (1)","High (5)"}; float[] amounts={0,1,5};
            for(int i=0;i<labels.Length;i++)
                if(Widgets.ButtonText(new Rect(row.x+i*row.width/3,row.y,row.width/3-3,28),labels[i]))
                { rule.BaseAmount=amounts[i]; rule.Enabled=amounts[i]>0; buffers.Remove(rule.Key+":amount"); }
        }

        private void DrawOrganDetails(Rect rect,Source source,AntiwealthSettings settings)
        {
            var rule=source.Rule;
            Rect view=new Rect(0,0,rect.width-20,Math.Max(rect.height,detailHeight));
            Widgets.BeginScrollView(rect,ref detailScroll,view);
            var listing=HelpUI.SingleColumn(new Listing_Standard()); listing.Begin(view);
            SectionTitle(listing,source.Name);
            HelpUI.Label(listing,"Maintained while kept; ends when the stock leaves.",
                "Only new natural organs harvested locally from prisoners qualify. Bought or older unmarked organs do not. The harvest's one-time Reputation gain is separate.");
            HelpUI.Equation(listing, "Stock", "Deterrence = stock value × multiplier",
                "Stock value is the combined market value of locally harvested prisoner organs currently held.");
            HelpUI.Label(listing,"Market-value multiplier: "+rule.BaseAmount.ToString("0.00")+"x",
                "0 removes the stock contribution; 0.75 is the default; 1 equals stock value; 5 gives five times the value. Colony size does not scale organ stock.");
            AmountInput(listing,rule.Key+":amount",ref rule.BaseAmount,0,5,0.05f,5);
            DrawStandardExample(listing,source,settings);
            if(Foldout(listing,"Preview",ref showPreview))
            {
                previewOrganValue=PreviewSlider(listing,"Example stock value",previewOrganValue,0,20000,100);
                listing.Label("Maintained deterrence: "+F(SourceMath.KeptOrgans(previewOrganValue,1,rule.Enabled?rule.BaseAmount:0)));
            }
            if(listing.ButtonText(inheritedSettings == null ? "Restore organ defaults" : "Use Global organ settings"))
            {
                var inherited = inheritedSettings?.Rule(rule.Key);
                rule.Enabled = inherited?.Enabled ?? true;
                rule.BaseAmount = inherited?.BaseAmount ?? 0.75f;
                ClearFactionRuleEdits(rule.Key, true);
                buffers.Remove(rule.Key+":amount");
            }
            detailHeight=listing.CurHeight+20; listing.End(); Widgets.EndScrollView();
        }

        private void DrawPopulation(Listing_Standard listing,EventRule rule)
        {
            HelpUI.Equation(listing, "ColonyFactor", "Colony factor = (colonists / reference size)^p",
                "Counts free colonists. Fixed ignores population. At the reference size the factor is 1. Applies to this source only.");
            Rect row=listing.GetRect(30);
            string[] labels={"Fixed","Square root","Linear"}; float[] powers={0,0.5f,1};
            for(int i=0;i<labels.Length;i++)
            {
                Rect button=new Rect(row.x+i*row.width/3,row.y,row.width/3-3,28);
                if(Widgets.ButtonText(button,labels[i])) { rule.PopulationExponent=powers[i]; buffers.Remove(rule.Key+":exponent"); }
                if(Math.Abs(rule.PopulationExponent-powers[i])<0.00001f) Widgets.DrawHighlightSelected(button);
            }
            HelpUI.Label(listing, "Population exponent (p): "+rule.PopulationExponent.ToString("0.##"),
                "0 = fixed; 0.5 = square root; 1 = linear. Four times the colonists gives twice the amount at exponent 0.5.");
            AmountInput(listing,rule.Key+":exponent",ref rule.PopulationExponent,0,2,0.05f,2);
            if(rule.PopulationExponent>0)
            {
                HelpUI.Label(listing,"Reference colony size","The configured amount applies at this many free colonists.");
                string buffer=Buffer(rule.Key+":reference",rule.ReferencePopulation.ToString());
                Widgets.TextFieldNumeric(listing.GetRect(30),ref rule.ReferencePopulation,ref buffer,1,1000);
                buffers[rule.Key+":reference"]=buffer;
            }
            DrawPreviewRow(listing.GetRect(25),new[]{"1 colonist","5 colonists","10 colonists","20 colonists"});
            var configured=new RuleValues { PopulationExponent=rule.PopulationExponent,ReferencePopulation=rule.ReferencePopulation };
            DrawPreviewRow(listing.GetRect(28),new[]{1,5,10,20}.Select(n=>configured.PopulationFactor(n).ToString("0.##")+"x").ToArray());
        }

        private void DrawStandardExample(Listing_Standard listing,Source source,AntiwealthSettings settings)
        {
            string caption = showColonyControls ? "Example at 5 colonists" : "Example";
            HelpUI.Label(listing,caption+": "+F(ExampleAmount(source,settings,DefaultExampleColonists))+" deterrence",
                ExampleDescription(source.Rule.Key)+". The calculation starts from zero and uses a sample colony of five. Summary uses the same example.");
            listing.Label(ExampleDescription(source.Rule.Key));
        }

        private void DrawSummary(Rect rect,AntiwealthSettings settings)
        {
            string intro=showColonyControls ? "Compare colony sizes. Select a source to edit."
                : "Select a source to edit its settings.";
            var header = HelpUI.SingleColumn(new Listing_Standard());
            header.Begin(rect);
            HelpUI.ThreatEquation(header, settings);
            HelpUI.Label(header, intro, "Examples use one body, one act, or one day of hazard exposure. Enable Colony size scaling in Formula to compare populations.");
            float introHeight = header.CurHeight;
            header.End();
            Rect heading=new Rect(rect.x,rect.y+introHeight,rect.width-20,28);
            var populations = showColonyControls ? new[]{1,5,10,20} : new[]{DefaultExampleColonists};
            string[] headings = showColonyControls ? new[]{"1 colonist","5 colonists","10 colonists","20 colonists"} : new[]{"Deterrence"};
            DrawComparisonRow(heading,"Source / example",headings);
            var sources=Sources(settings).Where(s=>!s.Extra || s.Rule.Enabled).ToList();
            var groups=sources.GroupBy(s=>RuleGrouping.SectionFor(s.Rule.Key)+" / "+s.Group).ToList();
            Rect outer=new Rect(rect.x,heading.yMax+5,rect.width,Math.Max(50,rect.yMax-heading.yMax-5));
            Rect view=new Rect(0,0,rect.width-20,Math.Max(outer.height,groups.Count*27+sources.Count*67));
            Widgets.BeginScrollView(outer,ref compareScroll,view);
            float y=0;
            foreach(var group in groups)
            {
                Rect groupRect=new Rect(0,y,view.width,25); Widgets.DrawLightHighlight(groupRect);
                Widgets.Label(groupRect.ContractedBy(3),group.Key); y+=27;
                foreach(var source in group)
                {
                    Rect row=new Rect(0,y,view.width,65); y+=67;
                    if(Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                    string state=SourceState(source,settings);
                    DrawComparisonRow(row,source.Name+"\n"+ExampleDescription(source.Rule.Key)+(state=="On"?"":"\n"+state),
                        populations.Select(n=>state=="On"?F(ExampleAmount(source,settings,n)):"Off").ToArray());
                    TooltipHandler.TipRegion(row,source.Name+"\n"+ExampleDescription(source.Rule.Key)+"\n"+state);
                    if(Widgets.ButtonInvisible(row))
                    { tab=(int)RuleGrouping.SectionFor(source.Rule.Key); SelectSource(source.Rule.Key); search=""; listScroll=Vector2.zero; }
                }
            }
            Widgets.EndScrollView();
        }

        private static bool SourceVisible(Source source,bool more,string query,string selection)
            => (!source.Extra || more || source.Rule.Key==selection || !string.IsNullOrWhiteSpace(query))
                && (string.IsNullOrWhiteSpace(query) || (source.Name+" "+source.Group).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);

        private static bool CategoryEnabled(Source source,AntiwealthSettings settings)
            => source.Rule.Key=="organs:local" || (RuleGrouping.IsBump(source.Rule.Key)?settings.Reputation
                : source.Rule.Key.StartsWith("remains:",StringComparison.Ordinal)?settings.Corpses:settings.Conditions);

        private static string SourceState(Source source,AntiwealthSettings settings)
        {
            if(!source.Installed) return source.Requirement??"Unavailable";
            if(!source.Rule.Enabled) return "Source off";
            if(!CategoryEnabled(source,settings)) return "Category off";
            if(source.Rule.BaseAmount<=0 && !(source.Rule.Key=="bump:human" && settings.IncludeHumanGear && settings.HumanGearFraction>0)) return "Zero amount";
            return "On";
        }

        private static RuleValues PreviewRule(Source source,AntiwealthSettings settings)
            => new RuleValues { Key=source.Rule.Key, BaseAmount=source.Rule.BaseAmount,
                PopulationExponent=source.Rule.PopulationExponent,ReferencePopulation=source.Rule.ReferencePopulation,
                Enabled=source.Installed && source.Rule.Enabled && CategoryEnabled(source,settings) };

        private static double ExampleAmount(Source source,AntiwealthSettings settings,int colonists)
        {
            var rule=PreviewRule(source,settings);
            if(!rule.Enabled) return 0;
            if(rule.Key=="organs:local") return SourceMath.KeptOrgans(1000,1,rule.BaseAmount);
            if(rule.Key.StartsWith("remains:",StringComparison.Ordinal)) return SourceMath.MaintainedRemains(100,rule,colonists);
            if(RuleGrouping.IsDeathBump(rule.Key)) return SourceMath.DeathBump(100,rule,colonists,
                rule.Key=="bump:human" && settings.IncludeHumanGear?1500:0,settings.HumanGearFraction);
            if(RuleGrouping.IsBump(rule.Key)) return rule.AmountFor(colonists);
            return settings.Growth.FromZero(rule.AmountFor(colonists), 1);
        }

        private static bool IsPerPower(string key)=>key.StartsWith("remains:",StringComparison.Ordinal)||RuleGrouping.IsDeathBump(key);
        private static string PopulationName(EventRule rule)=>rule.PopulationExponent==0?"fixed":rule.PopulationExponent==0.5f?"square root":rule.PopulationExponent==1?"linear":"custom";
        private static string ExampleDescription(string key)
        {
            if(key=="organs:local") return "Stock value 1,000";
            if(key.StartsWith("remains:",StringComparison.Ordinal)) return "One body, 100 combat power";
            if(key=="bump:human") return "100 power + 1,500 original gear";
            if(RuleGrouping.IsDeathBump(key)) return "One death, 100 combat power";
            if(RuleGrouping.IsBump(key)) return "One qualifying act";
            if(key=="pollution:ground") return "1 day, 100% polluted map";
            if(key=="infestation:hives") return "1 day, one hostile hive";
            if(key=="climate:heat") return "1 day, 10 C above threshold";
            if(key=="climate:cold") return "1 day, 10 C below threshold";
            return "One day active";
        }

        private static void DrawSourceEquation(Listing_Standard listing, string key, AntiwealthSettings settings)
        {
            string name, formula;
            if (key.StartsWith("remains:", StringComparison.Ordinal))
            { name = "Body"; formula = "Body = combat power × weight × colony factor"; }
            else if (key == "bump:human" && settings.IncludeHumanGear)
            { name = "Gear"; formula = "Gain = (power × weight + gear × share) × colony factor"; }
            else if (RuleGrouping.IsDeathBump(key))
            { name = "Death"; formula = "Gain = combat power × weight × colony factor"; }
            else if (RuleGrouping.IsBump(key))
            { name = "Act"; formula = "Gain = amount × colony factor"; }
            else
            { name = "Daily"; formula = "Daily rate = rate × exposure × colony factor"; }
            HelpUI.Equation(listing, name, formula, SourceHelp(key));
            if (key == "climate:heat" || key == "climate:cold")
                HelpUI.Equation(listing, key == "climate:heat" ? "HeatExposure" : "ColdExposure",
                    key == "climate:heat" ? "Exposure = degrees above threshold / 10" : "Exposure = degrees below threshold / 10",
                    EnvironmentHelp(key));
        }

        private static List<Source> Sources(AntiwealthSettings settings)=>BuildSources(settings,ModsConfig.BiotechActive,ModsConfig.IdeologyActive);

        private static List<Source> BuildSources(AntiwealthSettings settings,bool biotech,bool ideology)
        {
            var result=new List<Source>();
            foreach(var rule in settings.Rules)
            {
                if(!RuleGrouping.IsSupported(rule.Key)) continue;
                var source=new Source { Rule=rule };
                if(rule.Key.StartsWith("condition:",StringComparison.Ordinal))
                {
                    string name=rule.Key.Substring(10); var def=DefDatabase<GameConditionDef>.GetNamedSilentFail(name);
                    if (def == null) continue;
                    source.Name=name=="PsychicDroner" ? "Psychic droner" : name=="ToxicSpewer" ? "Toxic spewer" : def.LabelCap.ToString(); source.Unit="Deterrence / day while active";
                    source.Group="Conditions"; source.Order=40;
                    source.Extra=name!="ToxicFallout" && name!="ToxicRain" && name!="AcidRain"
                        && name!="PsychicDroner" && name!="ToxicSpewer"
                        && rule.BaseAmount<=0 && !rule.Enabled && !DefDatabase<AntiwealthConditionDef>.AllDefsListForReading.Any(d=>d.conditionDefName==name);
                }
                else if(rule.Key.StartsWith("remains:",StringComparison.Ordinal))
                {
                    source.Name=rule.Key=="remains:human"?"Human bodies":rule.Key=="remains:mech"?"Mechanoid remains":"Animal remains";
                    source.Unit="Deterrence / combat power"; source.Group="Bodies";
                    source.Order=rule.Key=="remains:human"?10:rule.Key=="remains:mech"?11:12;
                }
                else if(rule.Key=="organs:local")
                { source.Name="Kept prisoner organs"; source.Unit="Current market-value multiplier"; source.Group="Organs"; source.Order=20; }
                else if(RuleGrouping.IsBump(rule.Key))
                {
                    string kind=rule.Key.Substring(5);
                    source.Name=kind=="human"?"Hostile human death":kind=="mech"?"Hostile mechanoid death":kind=="animal"?"Hostile / manhunter animal death"
                        :kind=="organ"?"Prisoner organ harvest":kind=="failedSurgery"?"Failed removal surgery":kind=="amputation"?"Amputation (no organ)"
                        :kind=="execution"?"Prisoner execution":kind=="ritual"?"Ritual reward":HumanName(kind);
                    source.Group=RuleGrouping.IsDeathBump(rule.Key)?"Hostile deaths":kind=="ritual"?"Rituals":"Prisoner treatment";
                    source.Order=RuleGrouping.IsDeathBump(rule.Key)?(kind=="human"?10:kind=="mech"?11:12):kind=="ritual"?30
                        :kind=="organ"?20:kind=="failedSurgery"?21:kind=="amputation"?22:23;
                    source.Unit=RuleGrouping.IsDeathBump(rule.Key)?"Deterrence / combat power":"Deterrence / qualifying act";
                    source.Installed=kind!="ritual"||ideology; source.Requirement="Requires Ideology";
                }
                else
                {
                    switch(rule.Key)
                    {
                        case "pollution:ground": source.Name="Polluted ground"; source.Unit="Deterrence / day at 100% map pollution"; source.Group="Pollution"; source.Order=20; source.Installed=biotech; source.Requirement="Requires Biotech"; break;
                        case "infestation:hives": source.Name="Hostile infestation"; source.Unit="Deterrence / hostile hive / day"; source.Group="Infestation"; source.Order=30; break;
                        case "climate:heat": source.Name="Extreme outdoor heat"; source.Unit="Deterrence / day per 10 C above threshold"; source.Group="Outdoor temperature"; source.Order=10; break;
                        case "climate:cold": source.Name="Extreme outdoor cold"; source.Unit="Deterrence / day per 10 C below threshold"; source.Group="Outdoor temperature"; source.Order=11; break;
                        default: continue;
                    }
                }
                result.Add(source);
            }
            return result.OrderBy(s=>RuleGrouping.SectionFor(s.Rule.Key)).ThenBy(s=>s.Order).ThenBy(s=>s.Name).ToList();
        }

        private static string HumanName(string name)=>Regex.Replace(name,"([a-z])([A-Z])","$1 $2");
        private static string SourceHelp(string key)
        {
            if(key=="remains:animal") return "Hostile/manhunter animals only; ordinary hunted animals do not count. Rot does not reduce this amount.";
            if(key.StartsWith("remains:",StringComparison.Ordinal)) return "Hostile bodies count while kept, including graves and sarcophagi. Each body's fade is independent. Gear affects death gains on Reputation, not bodies.";
            if(key=="bump:ritual") return "Select reputation boost as an Ideology ritual's bonus reward. Positive outcomes add this gain; other rituals do not award it automatically.";
            if(key=="bump:organ") return "Successful local prisoner organ harvest, including lethal removal. The kept organ's stock value is configured under Remains.";
            if(key=="bump:failedSurgery") return "A failed prisoner organ-removal or amputation attempt. This earns its own fixed amount, not the harvest amount.";
            if(key=="bump:amputation") return "Successful prisoner removal without a harvested organ. One surgery earns one outcome.";
            if(key=="bump:execution") return "A completed prisoner execution. Prisoners do not also earn ordinary hostile-death gains.";
            if(RuleGrouping.IsDeathBump(key)) return "One gain per qualifying hostile death. Maintained body value is configured separately under Remains. Prisoner surgeries and executions have separate weights under Prisoner treatment.";
            return EnvironmentHelp(key);
        }
        private static string EnvironmentHelp(string key)
        {
            switch(key)
            {
                case "pollution:ground": return "Exposure is the polluted fraction of the map: 20% pollution gives 20% of the starting rate. Whole-map pollution counts.";
                case "condition:PsychicDroner": case "condition:ToxicSpewer": return "Earns the initial daily rate while this device affects the colony. Devices of the same type share a meter.";
                case "infestation:hives": return "Exposure is the hostile hive count. Clearing all hives starts this source's fade.";
                case "climate:heat": return "Every 10 °C above the heat threshold adds one unit of exposure. Exposure is zero at or below the threshold. Heat waves count through their temperature only.";
                case "climate:cold": return "Every 10 °C below the cold threshold adds one unit of exposure. Exposure is zero at or above the threshold. Cold snaps count through their temperature only.";
                default: return "One exposure unit while the condition is active. Gains slow with accumulated exposure. Repeated conditions share the surviving typed balance.";
            }
        }
    }
}
