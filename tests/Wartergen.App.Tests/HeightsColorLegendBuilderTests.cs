using System.Windows.Media;
using Wartergen.App.Models;
using Wartergen.App.Services;
using Wartergen.Bmp;

namespace Wartergen.App.Tests;

public class HeightsColorLegendBuilderTests
{
    [Fact]
    public void Build_ProducesMinDefaultMaxEntries()
    {
        IReadOnlyList<HeightsColorLegendEntry> legend = HeightsColorLegendBuilder.Build();

        Assert.Equal(3, legend.Count);
        Assert.Equal(["Min", "Default", "Max"], legend.Select(e => e.Label));
        Assert.Equal([HeightsPngConverter.MinHeight, HeightsPngConverter.DefaultHeight, HeightsPngConverter.MaxHeight],
            legend.Select(e => e.Height));
    }

    [Fact]
    public void Build_SwatchesRoundTripThroughHeightsPngConverter()
    {
        // Every legend swatch must decode back to the exact height it represents —
        // otherwise the legend would be showing the user colors that don't mean what it claims.
        foreach (HeightsColorLegendEntry entry in HeightsColorLegendBuilder.Build())
        {
            Color color = ((SolidColorBrush)entry.Brush).Color;
            var pixel = new RgbaColor(color.R, color.G, color.B, 255);

            int classified = HeightsPngConverter.Classify(pixel);

            Assert.Equal(entry.Height, classified);
        }
    }
}
