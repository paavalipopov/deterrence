using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Deterrence
{
    internal static class ViolenceExample
    {
        private static object Row(double day) => new {
            day,
            shared = PreviewMath.Bumps(day,2000,3,3,10,10000,0.5,FadeCurve.Cubic),
            single = PreviewMath.Bumps(day,2000,3,1,10,10000,0.5,FadeCurve.Cubic)
        };
        public static void Write(string path)
        {
            var times = new SortedSet<double>(Enumerable.Range(0,1001).Select(i=>i/50.0));
            times.Add(3-0.0001); times.Add(6-0.0001);
            var data = new { model = "Deterrence 0.5.0 shared violence meter", bump = 2000, interval = 3,
                count = 3, samples = times.Select(Row).ToArray(),
                milestones = new double[] { 0,3,6,9,12,16,20 }.Select(Row).ToArray() };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            File.WriteAllText(path,JsonSerializer.Serialize(data,new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine("Exported shared violence example: " + path);
        }
    }
}
