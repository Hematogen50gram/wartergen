using Wartergen.Bmp;

namespace Wartergen.Bmp.Tests;

public class WaterPngConverterTests
{
    [Fact]
    public void Classify_TransparentPixel_IsNull()
    {
        var pixel = new RgbaColor(255, 0, 0, 0); // color is irrelevant, only alpha matters

        Assert.Null(WaterPngConverter.Classify(pixel));
    }

    [Fact]
    public void Classify_AnyOpaqueOrPartiallyOpaquePixel_IsWater()
    {
        RgbaColor[] pixels =
        [
            new RgbaColor(255, 255, 255, 255), // opaque white
            new RgbaColor(0, 0, 0, 255),       // opaque black
            new RgbaColor(12, 200, 47, 1),     // barely-non-transparent
        ];

        foreach (RgbaColor pixel in pixels)
        {
            (int waterHeight, int flags)? classified = WaterPngConverter.Classify(pixel);
            Assert.Equal(8192, classified!.Value.waterHeight);
            Assert.Equal(256, classified.Value.flags);
        }
    }

    [Fact]
    public void Apply_OpaquePixelsOverwriteTheirTile()
    {
        int[] waterHeight = [24576, 24576];
        int[] flags = [0, 0];
        RgbaColor[] pixels = [new RgbaColor(0, 0, 255, 255), new RgbaColor(0, 0, 255, 255)];

        WaterPngConverter.Apply(waterHeight, flags, pixels);

        Assert.Equal([8192, 8192], waterHeight);
        Assert.Equal([256, 256], flags);
    }

    [Fact]
    public void Apply_TransparentPixelLeavesExistingTileUntouched()
    {
        int[] waterHeight = [9999, 24576];
        int[] flags = [123, 0];
        RgbaColor[] pixels = [new RgbaColor(0, 0, 0, 0), new RgbaColor(0, 0, 255, 255)]; // transparent, then opaque

        WaterPngConverter.Apply(waterHeight, flags, pixels);

        Assert.Equal([9999, 8192], waterHeight); // first tile untouched, second overwritten
        Assert.Equal([123, 256], flags);
    }

    [Fact]
    public void Apply_MismatchedLength_Throws()
    {
        int[] waterHeight = [24576, 24576, 24576];
        int[] flags = [0, 0, 0];
        RgbaColor[] pixels = [new RgbaColor(0, 0, 255, 255), new RgbaColor(0, 0, 0, 0)];

        Assert.Throws<InvalidOperationException>(() =>
            WaterPngConverter.Apply(waterHeight, flags, pixels));
    }
}
