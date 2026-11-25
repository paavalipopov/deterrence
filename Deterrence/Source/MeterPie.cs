using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace Deterrence
{
    internal sealed class MeterPie
    {
        private const int TextureSize = 192;
        private struct Slice
        {
            public double End;
            public Color32 Color;
            public string Caption, Tip;
        }

        private Slice[] slices = Array.Empty<Slice>();
        private Texture2D texture;
        private bool redraw = true;
        private const string EmptyTip = "This map currently has no deterrence.";

        internal static Color32 ColorFor(string key)
        {
            if (key == "hostile-remains") return new Color32(83, 185, 173, 255);
            if (key == "violence") return new Color32(182, 152, 221, 255);
            return new Color32(235, 161, 84, 255);
        }

        internal void Update(List<MeterNode> roots)
        {
            double total = roots.Sum(node => node.Credit);
            double accumulated = 0;
            if (slices.Length != roots.Count) { slices = new Slice[roots.Count]; redraw = true; }
            for (int i = 0; i < roots.Count; i++)
            {
                var node = roots[i];
                accumulated += node.Credit;
                double end = total > 0 ? accumulated / total : 0;
                double share = total > 0 ? node.Credit / total : 0;
                string percentage = share > 0 && share < 0.001 ? "<0.1%" : share.ToString("0.#%");
                var color = ColorFor(node.Key);
                if (slices[i].End != end || !slices[i].Color.Equals(color)) redraw = true;
                slices[i] = new Slice {
                    End = end, Color = color, Caption = node.Label + "   " + percentage,
                    Tip = total > 0 ? node.Label + " accounts for " + node.Credit.ToString("N0") + " of " + total.ToString("N0") + " deterrence (" + percentage + ")." : EmptyTip
                };
            }
        }

        // The same clockwise angle from twelve o'clock selects a texel and its hover hint.
        private int SliceAt(double x, double up)
        {
            double turn = Math.Atan2(x, up) / (2 * Math.PI);
            if (turn < 0) turn += 1;
            for (int i = 0; i < slices.Length; i++)
                if (turn < slices[i].End) return i;
            return -1;
        }

        private Color32[] ChartPixels()
        {
            var pixels = new Color32[TextureSize * TextureSize];
            double radius = TextureSize / 2.0 - 1;
            var empty = new Color32(128, 134, 142, 255);
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                double dx = x + 0.5 - TextureSize / 2.0, dy = y + 0.5 - TextureSize / 2.0;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                double coverage = Math.Min(1, radius + 0.5 - distance);
                if (coverage <= 0) continue;
                int index = SliceAt(dx, dy);
                Color32 ink = index >= 0 ? slices[index].Color : empty;
                if (index < 0) coverage *= Math.Max(0, Math.Min(1, distance - (radius - 5)));
                ink.a = (byte)Math.Round(255 * coverage);
                pixels[y * TextureSize + x] = ink;
            }
            return pixels;
        }

        internal void Draw(Rect area)
        {
            float diameter = Mathf.Min(96, area.height, area.width * 0.4f);
            var pie = new Rect(area.x, area.center.y - diameter / 2, diameter, diameter);
            if (Event.current.type == EventType.Repaint)
            {
                if (texture == null)
                {
                    texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true)
                        { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
                    redraw = true;
                }
                if (redraw) { texture.SetPixels32(ChartPixels()); texture.Apply(); redraw = false; }
                GUI.DrawTexture(pie, texture);
            }
            Vector2 offset = Event.current.mousePosition - pie.center;
            if (Mouse.IsOver(pie) && offset.sqrMagnitude <= diameter * diameter / 4)
            {
                int i = SliceAt(offset.x, -offset.y);
                TooltipHandler.TipRegion(pie, i < 0 ? EmptyTip : slices[i].Tip);
            }
            float line = Text.LineHeight;
            float left = pie.xMax + 12, top = area.center.y - slices.Length * line / 2;
            for (int i = 0; i < slices.Length; i++)
            {
                var row = new Rect(left, top + i * line, area.xMax - left, line);
                Widgets.DrawBoxSolid(new Rect(row.x, row.center.y - 4, 8, 8), slices[i].Color);
                Widgets.Label(new Rect(row.x + 14, row.y, row.width - 14, line), slices[i].Caption);
                TooltipHandler.TipRegion(row, slices[i].Tip);
            }
        }

        internal void Release()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
