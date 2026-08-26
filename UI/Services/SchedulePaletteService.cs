using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AutoScheduler.UI.Services
{
    // Çarşaf/program görünümlerinde ders/sınıf/öğretmen renklerini üretir. Sabit küçük bir
    // paletle (eskiden 8 renk, hash % 8) çakışma kaçınılmazdı; burada anahtar sayısı kadar
    // eşit aralıklı ton (hue) üretilerek çakışma olmadan renk ataması yapılır. UI (WPF Brush)
    // ve PDF export (r g b string) aynı index haritasını kullanarak tutarlı renk üretir.
    public static class SchedulePaletteService
    {
        public static Dictionary<string, int> BuildIndexMap(IEnumerable<string> keys)
        {
            var map = new Dictionary<string, int>();
            var index = 0;
            foreach (var key in (keys ?? Enumerable.Empty<string>())
                .Where(k => !string.IsNullOrWhiteSpace(k))
                .Distinct()
                .OrderBy(k => k, StringComparer.CurrentCultureIgnoreCase))
            {
                map[key] = index;
                index++;
            }
            return map;
        }

        public static (byte R, byte G, byte B) GetRgb(string key, IReadOnlyDictionary<string, int> indexMap)
        {
            if (string.IsNullOrWhiteSpace(key) || indexMap == null || indexMap.Count == 0 || !indexMap.TryGetValue(key, out var index))
                return (255, 255, 255);

            var hue = index * 360.0 / indexMap.Count % 360.0;
            return HslToRgb(hue, 0.55, 0.82);
        }

        public static string GetPdfColor(string key, IReadOnlyDictionary<string, int> indexMap)
        {
            var rgb = GetRgb(key, indexMap);
            return FormatInvariant(rgb.R / 255.0) + " " + FormatInvariant(rgb.G / 255.0) + " " + FormatInvariant(rgb.B / 255.0);
        }

        private static string FormatInvariant(double value)
        {
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static (byte R, byte G, byte B) HslToRgb(double h, double s, double l)
        {
            h /= 360.0;
            double r, g, b;

            if (s <= 0)
            {
                r = g = b = l;
            }
            else
            {
                var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
                var p = 2 * l - q;
                r = HueToRgb(p, q, h + 1.0 / 3.0);
                g = HueToRgb(p, q, h);
                b = HueToRgb(p, q, h - 1.0 / 3.0);
            }

            return ((byte)Math.Round(r * 255), (byte)Math.Round(g * 255), (byte)Math.Round(b * 255));
        }

        private static double HueToRgb(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6.0) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2.0) return q;
            if (t < 2.0 / 3.0) return p + (q - p) * (2.0 / 3.0 - t) * 6;
            return p;
        }
    }
}
