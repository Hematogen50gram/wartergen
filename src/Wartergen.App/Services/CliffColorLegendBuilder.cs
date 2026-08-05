using System.Windows.Media;
using Wartergen.App.Models;
using Wartergen.Bmp;

namespace Wartergen.App.Services;

// Builds display swatches matching CliffBmpConverter's decode rule (src/Wartergen.Bmp/CliffBmpConverter.cs):
// each height's hue comes from CliffBmpConverter.GetCanonicalHeightHue (the single source of truth
// for the height/hue mapping) and brightness selects cliffTexture (bright = 16, dark = 0). Canonical
// bright/dark values (1.0 / 0.35) sit clear of the 0.5 threshold either way.
public static class CliffColorLegendBuilder
{
    private const double BrightValue = 1.0;
    private const double DarkValue = 0.35;

    public static IReadOnlyList<CliffColorLegendEntry> Build()
    {
        var entries = new List<CliffColorLegendEntry>(CliffBmpConverter.HeightCount);

        for (int height = 0; height < CliffBmpConverter.HeightCount; height++)
        {
            double hue = CliffBmpConverter.GetCanonicalHeightHue(height);
            Color bright = HsvToColor(hue, saturation: 1.0, BrightValue);
            Color dark = HsvToColor(hue, saturation: 1.0, DarkValue);

            entries.Add(new CliffColorLegendEntry(height, ToFrozenBrush(bright), ToFrozenBrush(dark), ToHex(bright), ToHex(dark)));
        }

        return entries;
    }

    private static Color HsvToColor(double hueDegrees, double saturation, double value)
    {
        double c = value * saturation;
        double x = c * (1 - Math.Abs((hueDegrees / 60.0 % 2) - 1));
        double m = value - c;

        (double r, double g, double b) = hueDegrees switch
        {
            < 60 => (c, x, 0.0),
            < 120 => (x, c, 0.0),
            < 180 => (0.0, c, x),
            < 240 => (0.0, x, c),
            < 300 => (x, 0.0, c),
            _ => (c, 0.0, x)
        };

        return Color.FromRgb(ToByte(r + m), ToByte(g + m), ToByte(b + m));
    }

    private static byte ToByte(double channel) => (byte)Math.Round(Math.Clamp(channel, 0.0, 1.0) * 255.0);

    private static string ToHex(Color color) => $"#{color.R:X2}{color.G:X2}{color.B:X2}";

    private static Brush ToFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
