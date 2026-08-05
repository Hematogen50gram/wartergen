using System.Windows.Media;
using Wartergen.App.Models;
using Wartergen.Bmp;

namespace Wartergen.App.Services;

// Builds three reference swatches (min/default/max) for the heights legend, matching
// HeightsPngConverter.Classify's decode rule (src/Wartergen.Bmp/HeightsPngConverter.cs) so the
// UI always stays in sync with the actual grayscale-to-height mapping.
public static class HeightsColorLegendBuilder
{
    public static IReadOnlyList<HeightsColorLegendEntry> Build() =>
    [
        BuildEntry("Min", gray: 0, height: HeightsPngConverter.MinHeight),
        BuildEntry("Default", gray: DefaultGray, height: HeightsPngConverter.DefaultHeight),
        BuildEntry("Max", gray: 255, height: HeightsPngConverter.MaxHeight),
    ];

    // Inverse of HeightsPngConverter.Classify: which gray value round-trips to DefaultHeight.
    private static readonly byte DefaultGray = (byte)Math.Round(
        (HeightsPngConverter.DefaultHeight - HeightsPngConverter.MinHeight)
        / (double)(HeightsPngConverter.MaxHeight - HeightsPngConverter.MinHeight) * 255.0);

    private static HeightsColorLegendEntry BuildEntry(string label, byte gray, int height)
    {
        var color = Color.FromRgb(gray, gray, gray);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        string hex = $"#{gray:X2}{gray:X2}{gray:X2}";
        return new HeightsColorLegendEntry(label, gray, height, brush, hex);
    }
}
