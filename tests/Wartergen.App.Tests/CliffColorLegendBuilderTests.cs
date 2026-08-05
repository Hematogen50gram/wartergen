using System.Windows.Media;
using Wartergen.App.Models;
using Wartergen.App.Services;
using Wartergen.Bmp;

namespace Wartergen.App.Tests;

public class CliffColorLegendBuilderTests
{
    [Fact]
    public void Build_ProducesOneEntryPerHeight()
    {
        IReadOnlyList<CliffColorLegendEntry> legend = CliffColorLegendBuilder.Build();

        Assert.Equal(15, legend.Count);
        Assert.Equal(Enumerable.Range(0, 15), legend.Select(e => e.Height));
    }

    [Fact]
    public void Build_SwatchesRoundTripThroughCliffBmpConverter()
    {
        // Every legend swatch must decode back to the exact height/texture it represents —
        // otherwise the legend would be showing the user colors that don't mean what it claims.
        foreach (CliffColorLegendEntry entry in CliffColorLegendBuilder.Build())
        {
            RgbColor bright = ToRgb(entry.BrightBrush);
            RgbColor dark = ToRgb(entry.DarkBrush);

            (int CliffTexture, int LayerHeight) brightClassified = CliffBmpConverter.Classify(bright);
            (int CliffTexture, int LayerHeight) darkClassified = CliffBmpConverter.Classify(dark);

            Assert.Equal(16, brightClassified.CliffTexture);
            Assert.Equal(entry.Height, brightClassified.LayerHeight);
            Assert.Equal(0, darkClassified.CliffTexture);
            Assert.Equal(entry.Height, darkClassified.LayerHeight);
        }
    }

    private static RgbColor ToRgb(Brush brush)
    {
        Color color = ((SolidColorBrush)brush).Color;
        return new RgbColor(color.R, color.G, color.B);
    }
}
