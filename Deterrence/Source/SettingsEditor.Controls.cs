using System;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public partial class SettingsEditor
    {
        private void SelectSource(string key)
        {
            selected = key; detailScroll = Vector2.zero; detailHeight = 850;
            showPopulation = false; showPreview = false; previewExposure = 1;
        }

        private static void SectionTitle(Listing_Standard listing, string text)
        {
            listing.Gap(6);
            Text.Font = GameFont.Medium; listing.Label(text); Text.Font = GameFont.Small;
        }

        private void AmountInput(Listing_Standard listing, string key, ref float value, float min,
            float max, float step, float sliderMax, string help = null)
        {
            float start = listing.CurHeight;
            Rect row = listing.GetRect(32);
            string buffer = Buffer(key, value.ToString());
            bool minus = Widgets.ButtonText(new Rect(row.x, row.y, 30, 28), "-");
            bool plus = Widgets.ButtonText(new Rect(row.xMax - 30, row.y, 30, 28), "+");
            Widgets.TextFieldNumeric(new Rect(row.x + 36, row.y, row.width - 72, 28), ref value, ref buffer, min, max);
            if (minus || plus)
            {
                value = Mathf.Clamp(value + (plus ? step : -step), min, max);
                buffer = value.ToString();
            }
            buffers[key] = buffer;
            // A saved non-round value must not change merely because its page is opened.
            float next = listing.Slider(value, Math.Min(min, value), Math.Max(sliderMax, value));
            if (help != null) TooltipHandler.TipRegion(new Rect(row.x, row.y, row.width, listing.CurHeight - start), help);
            if (Math.Abs(next - value) > 0.000001f)
            {
                value = Mathf.Clamp(Mathf.Round(next / step) * step, min, max);
                buffers.Remove(key);
            }
        }

        private void GlobalSlider(Listing_Standard listing, string label, ref float value, float min,
            float max, float step, bool fraction = false, string symbol = null)
        {
            string help;
            switch (label)
            {
                case "Smoothed surface wealth": help = "Multiplier on smoothed floors and walls in Base threat. At 1 they keep their usual contribution. At 0 their contribution is zero."; break;
                case "Exposure scale": help = "Raw exposure accumulates before slowdown. Increasing this scale lets each hazard grow at its starting pace for longer."; break;
                case "Slowdown days at 2,000/day": help = "The slowdown time at the reference daily rate. Larger values delay slowdown. At logarithmic shape this is the half-speed day."; break;
                case "Growth shape": help = "0 is linear, 0.5 is square-root growth, and 1 is logarithmic growth."; break;
                case "Reference balance": help = "A balance of this size fades in the reference duration. Larger balances last longer when the exponent is positive."; break;
                case "Reference area (point-days)": help = "A balance of 10,000 held for two days contributes 20,000 point-days of history."; break;
                case "Duration exponent": help = "0 gives a fixed duration. At 0.5, four times the balance or area gives twice the fade time. At 1, duration scales proportionally."; break;
                case "Gear value share": help = "Fraction of the original weapons and worn-apparel market value added to a hostile human death gain. Maintained bodies do not include gear."; break;
                case "Biome strength": help = "Raise the biome settlement weight to this exponent to get the biome factor. At 0 the factor is 1, at 0.5 it is the square root, and at 1 it equals the weight."; break;
                case "Island multiplier": help = "The multiplier for a qualifying island. At 1 the full base counts. At 0 the wealth contribution is zero. Other raid factors still apply."; break;
                case "Maximum island tiles": help = "A water-enclosed landmass can qualify if it has at most this many world map tiles. The default is 180."; break;
                case "Heat threshold (C)": help = "Outdoor temperature above this value creates heat exposure. Every additional 10 C is one unit. Below the threshold there is no new heat gain."; break;
                case "Cold threshold (C)": help = "Outdoor temperature below this value creates cold exposure. Every additional 10 C is one unit. Above the threshold there is no new cold gain."; break;
                case "Raid skip strength": help = "Human enemy raids are called off with chance s × (1 − r)^p, using r for the raiding faction's profile. Quest raids are unaffected. At 0 raids keep their usual timing."; break;
                case "Skip exponent": help = "Shapes both skip chances. Above 1, mild deterrence barely changes how often outsiders come. At 1 the chance is proportional to 1 − r."; break;
                case "Retreat point at reference reputation": help = "Raiders normally retreat after losing 40–70% of their group, depending on the faction. At the reference balance on the Curves tab, they retreat at this share of their usual point. With no reputation they keep the usual point. At 100% nothing changes."; break;
                case "Weight of a faction's own dead": help = "When a faction raids, the remains of its own members count this many times before the remains curve. At 1 every body counts the same."; break;
                default: help = "Changes this source's contribution."; break;
            }
            string display = fraction ? value.ToString("P0")
                : label == "Growth shape" || label == "Duration exponent" || label == "Slowdown days at 2,000/day" || label == "Skip exponent"
                    ? value.ToString("0.##") : label == "Biome strength" || label == "Island multiplier" || label == "Smoothed surface wealth" ? value.ToString("0.00") : F(value);
            string caption = symbol == null ? label : label + " (" + symbol + ")";
            HelpUI.Label(listing, caption + ": " + display, help);
            float visualMax = label == "Exposure scale" || label == "Reference balance" || label == "Reference area (point-days)" ? 100000 : max;
            AmountInput(listing, "global:" + label, ref value, min, max, step, visualMax, help);
        }

        private static float PreviewSlider(Listing_Standard listing, string label, float value,
            float min, float max, float step)
        {
            listing.Label(label + ": " + F(value));
            float next = listing.Slider(value, Math.Min(min, value), Math.Max(max, value));
            return Math.Abs(next-value) > 0.000001f ? Mathf.Clamp(Mathf.Round(next/step)*step,min,max) : value;
        }
    }
}
