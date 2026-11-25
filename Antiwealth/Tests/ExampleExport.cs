using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Antiwealth
{
    internal static class ExampleExport
    {
        private static double Amount(double day, double start, double end, double rate)
        {
            if (day <= start) return 0;
            double peak = CreditMath.Accumulated(rate * (end - start), 20000, AccumulationCurve.Logarithmic);
            return day < end ? CreditMath.Accumulated(rate * (day - start), 20000, AccumulationCurve.Logarithmic)
                : CreditMath.Remaining(peak, day - end, CreditMath.Duration(peak, 10, 10000));
        }

        private static object Row(double day)
        {
            double first = Amount(day, 0, 6, 4000);
            double second = Amount(day, 3, 11, 2000);
            double total = first + second;
            double biome = SourceMath.BiomeMultiplier(0.6, 0.5, true);
            return new
            {
                day, fallout = first, hive = second, total,
                vanilla = 100000, biomeOnly = 100000 * biome,
                adjusted = CreditMath.EffectiveWealth(100000, total) * biome
            };
        }

        public static void Write(string path)
        {
            double a = CreditMath.Accumulated(4000 * 6, 20000, AccumulationCurve.Logarithmic);
            double b = CreditMath.Accumulated(2000 * 8, 20000, AccumulationCurve.Logarithmic);
            var document = new
            {
                model = "Antiwealth 0.5.0 - exported from the actual calculation source",
                note = "Environmental reputation only. No violence bumps, remains, loot, or changing colony wealth.",
                curve = "logarithmic", slowdownScale = 20000, referenceDays = 10, referenceCredit = 10000,
                sourceA = new { name = "Toxic fallout", start = 0, end = 6, initialRate = 4000, peak = a, fadeEnd = 6 + CreditMath.Duration(a, 10, 10000) },
                sourceB = new { name = "One hostile hive", start = 3, end = 11, initialRate = 2000, peak = b, fadeEnd = 11 + CreditMath.Duration(b, 10, 10000) },
                samples = Enumerable.Range(0, 1301).Select(i => Row(i / 50.0)).ToArray(),
                milestones = new double[] { 0, 3, 6, 9, 11, 16, 22, 26 }.Select(Row).ToArray()
            };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Exported model data: " + path);
        }
    }
}
