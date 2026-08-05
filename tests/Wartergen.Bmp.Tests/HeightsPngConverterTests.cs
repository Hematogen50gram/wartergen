using Wartergen.Bmp;

namespace Wartergen.Bmp.Tests;

public class HeightsPngConverterTests
{
    [Fact]
    public void Classify_Black_IsMinHeight()
    {
        var pixel = new RgbaColor(0, 0, 0, 255);

        Assert.Equal(HeightsPngConverter.MinHeight, HeightsPngConverter.Classify(pixel));
    }

    [Fact]
    public void Classify_White_IsMaxHeight()
    {
        var pixel = new RgbaColor(255, 255, 255, 255);

        Assert.Equal(HeightsPngConverter.MaxHeight, HeightsPngConverter.Classify(pixel));
    }

    [Fact]
    public void Classify_OneThirdGray_IsLinearlyInterpolated()
    {
        // 85/255 = 1/3 exactly, and (16383-7680)/3 = 2901 exactly, so this avoids rounding ambiguity.
        var pixel = new RgbaColor(85, 85, 85, 255);

        Assert.Equal(7680 + 2901, HeightsPngConverter.Classify(pixel));
    }

    [Fact]
    public void Classify_IgnoresAlphaAndGreenBlue()
    {
        RgbaColor[] pixels =
        [
            new RgbaColor(100, 0, 0, 255),
            new RgbaColor(100, 255, 255, 255),
            new RgbaColor(100, 50, 200, 0),
        ];

        int[] classified = [.. pixels.Select(HeightsPngConverter.Classify)];

        Assert.All(classified, value => Assert.Equal(classified[0], value));
    }

    [Fact]
    public void Apply_EveryPixelOverwritesItsTile()
    {
        int[] groundHeight = [9999, 9999];
        RgbaColor[] pixels = [new RgbaColor(0, 0, 0, 255), new RgbaColor(255, 255, 255, 255)];

        HeightsPngConverter.Apply(groundHeight, pixels);

        Assert.Equal([HeightsPngConverter.MinHeight, HeightsPngConverter.MaxHeight], groundHeight);
    }

    [Fact]
    public void Apply_MismatchedLength_Throws()
    {
        int[] groundHeight = [8192, 8192, 8192];
        RgbaColor[] pixels = [new RgbaColor(0, 0, 0, 255), new RgbaColor(255, 255, 255, 255)];

        var ex = Assert.Throws<InvalidOperationException>(() =>
            HeightsPngConverter.Apply(groundHeight, pixels));
        Assert.Contains("must match exactly", ex.Message);
    }
}
