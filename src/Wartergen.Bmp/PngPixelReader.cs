using System.Drawing;
using System.Drawing.Imaging;

namespace Wartergen.Bmp;

// Reads a PNG's pixels row-major, top-left first, preserving alpha. Shared by CliffPngConverter
// and WaterPngConverter — both need alpha-aware pixel reads, unlike BmpGroundTextureConverter
// (which only ever deals with opaque BMPs).
public static class PngPixelReader
{
    // Converts to Format32bppArgb via Bitmap.Clone (a pixel-format conversion) rather than
    // Graphics.DrawImage — DrawImage would alpha-composite the source over a blank transparent
    // canvas, which discards the RGB of any fully-transparent source pixel (result would be
    // (0,0,0,0) regardless of the source's color). That matters wherever a transparent pixel's
    // color still needs to be read (e.g. CliffPngConverter's hue-based height classification,
    // which reads hue independently of alpha).
    public static RgbaColor[] ReadPixelsTopLeftFirst(string pngPath)
    {
        using var source = new Bitmap(pngPath);
        using Bitmap argbBitmap = source.Clone(new Rectangle(0, 0, source.Width, source.Height), PixelFormat.Format32bppArgb);

        var pixels = new RgbaColor[argbBitmap.Width * argbBitmap.Height];
        int index = 0;
        for (int y = 0; y < argbBitmap.Height; y++)
        {
            for (int x = 0; x < argbBitmap.Width; x++)
            {
                Color color = argbBitmap.GetPixel(x, y);
                pixels[index++] = new RgbaColor(color.R, color.G, color.B, color.A);
            }
        }

        return pixels;
    }
}
