using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;

namespace Deterrence
{
    // Sum of glyph coverage above and to the left of each pixel edge. Integrating
    // over a destination pixel gives one antialiased sample at its actual size.
    internal sealed class EquationImage
    {
        public readonly int Width, Height;
        private readonly int[] sums;
        private readonly Dictionary<Vector2Int, Texture2D> sizes = new Dictionary<Vector2Int, Texture2D>();
        private readonly Queue<Vector2Int> recent = new Queue<Vector2Int>();

        private EquationImage(int width, int height, Color32[] pixels)
        {
            Width = width;
            Height = height;
            int stride = Width + 1;
            sums = new int[stride * (Height + 1)];
            for (int y = 0; y < Height; y++)
            {
                int row = 0;
                for (int x = 0; x < Width; x++)
                {
                    row += pixels[y * Width + x].a;
                    sums[(y + 1) * stride + x + 1] = sums[y * stride + x + 1] + row;
                }
            }
        }

        public static EquationImage Load(string name)
        {
            string path = Path.Combine(DeterrenceMod.EquationDirectory, name + ".png");
            if (!File.Exists(path)) return null;
            var master = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                return ImageConversion.LoadImage(master, File.ReadAllBytes(path))
                    ? new EquationImage(master.width, master.height, master.GetPixels32()) : null;
            }
            catch (IOException error)
            {
                Log.Warning("[Deterrence] Could not read equation " + name + ": " + error.Message);
                return null;
            }
            finally { UnityEngine.Object.Destroy(master); }
        }

        private double Integral(double x, double y)
        {
            int left = Math.Min((int)x, Width - 1), bottom = Math.Min((int)y, Height - 1);
            double dx = x - left, dy = y - bottom;
            int i = bottom * (Width + 1) + left;
            double lower = sums[i] + dx * (sums[i + 1] - sums[i]);
            i += Width + 1;
            double upper = sums[i] + dx * (sums[i + 1] - sums[i]);
            return lower + dy * (upper - lower);
        }

        private Color32[] Rasterize(int width, int height)
        {
            double sx = (double)Width / width, sy = (double)Height / height;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                double low = y * sy, high = (y + 1) * sy, previous = 0;
                for (int x = 0; x < width; x++)
                {
                    double right = (x + 1) * sx;
                    double next = Integral(right, high) - Integral(right, low);
                    int alpha = (int)Math.Round((next - previous) / (sx * sy));
                    pixels[y * width + x] = new Color32(255, 255, 255, (byte)Math.Max(0, Math.Min(255, alpha)));
                    previous = next;
                }
            }
            return pixels;
        }

        public Texture2D AtSize(int width, int height)
        {
            var key = new Vector2Int(width, height);
            if (sizes.TryGetValue(key, out var cached)) return cached;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(Rasterize(width, height));
            texture.Apply(false, true);
            if (recent.Count == 4)
            {
                var oldest = recent.Dequeue();
                UnityEngine.Object.Destroy(sizes[oldest]);
                sizes.Remove(oldest);
            }
            recent.Enqueue(key);
            sizes.Add(key, texture);
            return texture;
        }
    }
}
