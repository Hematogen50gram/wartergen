using System.Drawing;
using System.Drawing.Imaging;
using Wartergen.Bmp;

namespace Wartergen.Bmp.Tests;

public class PngPixelReaderTests
{
    [Fact]
    public void ReadPixelsTopLeftFirst_ReadsRowMajorTopLeftFirstWithAlpha()
    {
        string path = Path.Combine(Path.GetTempPath(), $"wartergen-test-{Guid.NewGuid():N}.png");
        try
        {
            using (var bitmap = new Bitmap(2, 2))
            {
                bitmap.SetPixel(0, 0, Color.FromArgb(255, 255, 0, 0));   // top-left: opaque red
                bitmap.SetPixel(1, 0, Color.FromArgb(255, 0, 255, 0));   // top-right: opaque green
                bitmap.SetPixel(0, 1, Color.FromArgb(0, 0, 0, 255));     // bottom-left: transparent blue
                bitmap.SetPixel(1, 1, Color.FromArgb(128, 255, 255, 255)); // bottom-right: half-transparent white
                bitmap.Save(path, ImageFormat.Png);
            }

            RgbaColor[] pixels = PngPixelReader.ReadPixelsTopLeftFirst(path);

            Assert.Equal(
            [
                new RgbaColor(255, 0, 0, 255),
                new RgbaColor(0, 255, 0, 255),
                new RgbaColor(0, 0, 255, 0),
                new RgbaColor(255, 255, 255, 128)
            ], pixels);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
