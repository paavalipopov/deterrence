using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Deterrence
{
    public static class HelpUI
    {
        private static readonly Dictionary<string, EquationImage> equations = new Dictionary<string, EquationImage>();

        public static Listing_Standard SingleColumn(Listing_Standard listing)
        {
            listing.maxOneColumn = true;
            return listing;
        }

        public static void Label(Listing_Standard listing, string label, string help, bool prominent = false, bool centered = false)
        {
            var previousFont = Text.Font;
            var previousAnchor = Text.Anchor;
            if (prominent) Text.Font = GameFont.Medium;
            float width = listing.ColumnWidth;
            Rect rect = listing.GetRect(Mathf.Max(28, Text.CalcHeight(label, width)));
            if (centered) Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(rect, label);
            Text.Font = previousFont;
            Text.Anchor = previousAnchor;
            TooltipHandler.TipRegion(rect, help);
        }

        internal static void ThreatEquation(Listing_Standard listing, DeterrenceSettings settings, bool centered = false)
        {
            string name = "Threat" + (settings.Enabled ? "Deterrence" : "")
                + (settings.BiomeWealthScaling ? "Biome" : "") + (settings.IslandWealthScaling ? "Island" : "");
            Equation(listing, name, SourceMath.ThreatWealthFormula(settings.Enabled, settings.BiomeWealthScaling, settings.IslandWealthScaling),
                "Base is the colony's wealth input after Misc adjustments. The storyteller uses Threat along with population, adaptation, and difficulty to calculate threat points.", centered: centered);
        }

        private static EquationImage EquationTexture(string name)
        {
            if (!equations.TryGetValue(name, out var texture))
            {
                texture = EquationImage.Load(name);
                equations.Add(name, texture);
            }
            return texture;
        }

        // All equation canvases have a baseline at 22/32 of their height.
        private static float EquationHeight(EquationImage texture, float availableWidth)
            => Mathf.Min(2f * Text.LineHeight, Mathf.Max(0, availableWidth) * texture.Height / texture.Width);

        private const float NoteGap = 18f;
        private static GUIStyle fadeNote;

        private static void DrawEquationLine(Rect area, EquationImage texture, float height, string suffix, float suffixWidth, bool centered, bool annotation = false, GUIStyle noteStyle = null)
        {
            float width = height * texture.Width / texture.Height;
            float left = centered ? area.center.x - (width + suffixWidth) / 2 : area.x;
            float top = area.center.y - height / 2;
            if (Event.current.type != EventType.Repaint || height <= 0) return;
            var pixel = GUIUtility.GUIToScreenPoint(new Vector2(left, top));
            var far = GUIUtility.GUIToScreenPoint(new Vector2(left + width, top + height));
            var size = new Vector2(Mathf.Round(far.x - pixel.x), Mathf.Round(far.y - pixel.y));
            int w = (int)Mathf.Abs(size.x), h = (int)Mathf.Abs(size.y);
            if (w == 0 || h == 0) return;
            pixel = new Vector2(Mathf.Round(pixel.x), Mathf.Round(pixel.y));
            var origin = GUIUtility.ScreenToGUIPoint(pixel);
            var end = GUIUtility.ScreenToGUIPoint(pixel + size);
            GUI.DrawTexture(Rect.MinMaxRect(origin.x, origin.y, end.x, end.y), texture.AtSize(w, h));
            if (suffix != null)
            {
                float baseline = origin.y + height * (22f / 32f);
                float gap = annotation ? NoteGap : 4f;
                if (annotation)
                    Widgets.DrawBoxSolid(new Rect(origin.x + width + gap / 2, area.center.y - 7, 1, 14),
                        new Color(1f, 1f, 1f, 0.32f));
                float noteHeight = noteStyle == null ? Text.LineHeight : Mathf.Ceil(noteStyle.CalcSize(new GUIContent(suffix)).y);
                Rect note = new Rect(origin.x + width + gap, baseline - noteHeight * 0.75f, suffixWidth - gap, noteHeight);
                if (noteStyle == null) Widgets.Label(note, suffix);
                else GUI.Label(note, suffix, noteStyle);
            }
        }

        private static void WrappedNote(Listing_Standard listing, string suffix, string help, GUIStyle style)
        {
            if (style == null) { listing.Label(suffix, tooltip: help); return; }
            var content = new GUIContent(suffix);
            Rect row = listing.GetRect(Mathf.Ceil(style.CalcHeight(content, listing.ColumnWidth)));
            GUI.Label(row, content, style);
            TooltipHandler.TipRegion(row, help);
        }

        internal static void Equation(Listing_Standard listing, string name, string fallback, string help, string suffix = null, bool centered = false, bool largeNote = false)
        {
            GUIStyle style = largeNote ? (fadeNote ??= new GUIStyle(Text.CurFontStyle) { fontSize = 17, wordWrap = true }) : null;
            var texture = EquationTexture(name);
            if (texture == null)
            {
                Label(listing, fallback, help, true, centered);
                if (suffix != null) WrappedNote(listing, suffix, help, style);
                return;
            }
            float suffixWidth = suffix == null ? 0 : Mathf.Ceil(style == null ? Text.CalcSize(suffix).x
                : style.CalcSize(new GUIContent(suffix)).x) + NoteGap + 2;
            float available = listing.ColumnWidth;
            float height = EquationHeight(texture, available);
            float width = height * texture.Width / texture.Height;
            bool wrapSuffix = suffix != null && width + suffixWidth > available;
            Rect row = listing.GetRect(Mathf.Max(28, height));
            DrawEquationLine(row, texture, height, wrapSuffix ? null : suffix, wrapSuffix ? 0 : suffixWidth, centered, annotation: true, noteStyle: style);
            TooltipHandler.TipRegion(row, help);
            if (wrapSuffix) WrappedNote(listing, suffix, help, style);
        }

        internal static void MeterFactor(Rect area, string name, string label, double value)
        {
            var texture = EquationTexture(name);
            string percent = value.ToString("P0");
            if (texture == null) Widgets.Label(area, label + ": " + percent);
            else
            {
                float suffixWidth = Mathf.Ceil(Text.CalcSize(percent).x) + 6;
                float height = Mathf.Min(area.height, EquationHeight(texture, area.width - suffixWidth));
                DrawEquationLine(area, texture, height, percent, suffixWidth, false);
            }
            TooltipHandler.TipRegion(area, label);
        }

        public static void Checkbox(Listing_Standard listing, string label, ref bool value, string help, bool prominent = false)
        {
            var previousFont = Text.Font;
            if (prominent) Text.Font = GameFont.Medium;
            Rect rect = listing.GetRect(Mathf.Max(32, Text.CalcHeight(label, listing.ColumnWidth - 28) + 4));
            Widgets.CheckboxLabeled(rect, label, ref value);
            Text.Font = previousFont;
            TooltipHandler.TipRegion(rect, help);
        }
    }
}
