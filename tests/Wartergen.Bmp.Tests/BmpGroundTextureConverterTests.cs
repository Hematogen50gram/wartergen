using System.Drawing;
using Wartergen.Bmp;

namespace Wartergen.Bmp.Tests;

public class BmpGroundTextureConverterTests
{
    [Fact]
    public void BuildConvertedTexture_AssignsIndicesInFirstAppearanceOrder()
    {
        var red = new RgbColor(255, 0, 0);
        var green = new RgbColor(0, 255, 0);
        var blue = new RgbColor(0, 0, 255);

        // red, green, red, blue, green -> first-appearance order is red=0, green=1, blue=2
        RgbColor[] pixels = [red, green, red, blue, green];

        int[] converted = BmpGroundTextureConverter.BuildConvertedTexture(pixels);

        Assert.Equal([0, 1, 0, 2, 1], converted);
    }

    [Fact]
    public void ApplyGroundTexture_ZeroFillsWhenConvertedTextureShorterThanGroundTexture()
    {
        int[] groundTexture = [9, 9, 9, 9, 9];
        int[] convertedTexture = [3, 4];

        BmpGroundTextureConverter.ApplyGroundTexture(groundTexture, convertedTexture);

        Assert.Equal([3, 4, 0, 0, 0], groundTexture);
    }

    [Fact]
    public void ApplyGroundTexture_IgnoresExcessConvertedValuesBeyondGroundTextureLength()
    {
        int[] groundTexture = [9, 9];
        int[] convertedTexture = [3, 4, 5, 6];

        BmpGroundTextureConverter.ApplyGroundTexture(groundTexture, convertedTexture);

        Assert.Equal([3, 4], groundTexture);
    }

    [Fact]
    public void ReadPixelsTopLeftFirst_ReadsRowMajorTopLeftFirst()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wartergen-test-{Guid.NewGuid():N}.bmp");
        try
        {
            using (var bitmap = new Bitmap(2, 2))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(255, 0, 0));   // top-left: red
                bitmap.SetPixel(1, 0, Color.FromArgb(0, 255, 0));   // top-right: green
                bitmap.SetPixel(0, 1, Color.FromArgb(0, 0, 255));   // bottom-left: blue
                bitmap.SetPixel(1, 1, Color.FromArgb(255, 255, 255)); // bottom-right: white
                bitmap.Save(path);
            }

            RgbColor[] pixels = BmpGroundTextureConverter.ReadPixelsTopLeftFirst(path);

            Assert.Equal(
            [
                new RgbColor(255, 0, 0),
                new RgbColor(0, 255, 0),
                new RgbColor(0, 0, 255),
                new RgbColor(255, 255, 255)
            ], pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
