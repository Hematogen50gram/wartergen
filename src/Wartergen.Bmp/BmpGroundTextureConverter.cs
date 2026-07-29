using System.Drawing;
using System.Drawing.Imaging;

namespace Wartergen.Bmp;

public static class BmpGroundTextureConverter
{
    // Reads pixels row-major, top-left first (matches PIL's img.convert("RGB").getdata() order).
    // Converts to a non-indexed 24bpp bitmap first so GetPixel works uniformly regardless of the
    // source BMP's pixel format (e.g. 8-bit palette-indexed BMPs, which GetPixel can't read directly).
    public static RgbColor[] ReadPixelsTopLeftFirst(string bmpPath)
    {
        using var source = new Bitmap(bmpPath);
        using var rgbBitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
        using (Graphics graphics = Graphics.FromImage(rgbBitmap))
        {
            graphics.DrawImage(source, 0, 0, source.Width, source.Height);
        }

        var pixels = new RgbColor[rgbBitmap.Width * rgbBitmap.Height];
        int index = 0;
        for (int y = 0; y < rgbBitmap.Height; y++)
        {
            for (int x = 0; x < rgbBitmap.Width; x++)
            {
                Color color = rgbBitmap.GetPixel(x, y);
                pixels[index++] = new RgbColor(color.R, color.G, color.B);
            }
        }

        return pixels;
    }

    // Assigns each unique color a sequential index in first-appearance order (matches the
    // Python script's color_to_index dict), then maps every pixel to its color's index.
    public static int[] BuildConvertedTexture(IReadOnlyList<RgbColor> pixels)
    {
        var colorToIndex = new Dictionary<RgbColor, int>();
        var convertedTexture = new int[pixels.Count];

        for (int i = 0; i < pixels.Count; i++)
        {
            RgbColor color = pixels[i];
            if (!colorToIndex.TryGetValue(color, out int index))
            {
                index = colorToIndex.Count;
                colorToIndex[color] = index;
            }
            convertedTexture[i] = index;
        }

        return convertedTexture;
    }

    // Positional overwrite of groundTexture: zero-fills any entries beyond the converted
    // texture's length, and silently ignores any excess converted values beyond groundTexture's
    // length. No dimension/palette-size validation, matching populate_ground_texture exactly.
    public static void ApplyGroundTexture(int[] groundTexture, int[] convertedTexture)
    {
        for (int i = 0; i < groundTexture.Length; i++)
        {
            groundTexture[i] = i < convertedTexture.Length ? convertedTexture[i] : 0;
        }
    }
}
