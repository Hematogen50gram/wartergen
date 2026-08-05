namespace Wartergen.Bmp;

// Overlays a user-painted water PNG (read via PngPixelReader) onto war3map.w3e's waterHeight and
// flags arrays, in place, tile by tile. Per pixel: alpha == 0 means the pixel is fully transparent
// and that tile's existing waterHeight/flags are left untouched — any other alpha means water,
// regardless of RGB (unlike cliffs, water has no further sub-classification: a single level, not
// distinct "types"). A water tile also needs its flags bit set (256) alongside waterHeight —
// waterHeight alone isn't enough for the map to actually render water there.
public static class WaterPngConverter
{
    private const int Water = 8192;
    private const int WaterFlag = 256;

    // Classifies a single pixel into (waterHeight, flags), or null if the pixel is fully
    // transparent — meaning "leave this tile's existing map data untouched", not "no water".
    public static (int WaterHeight, int Flags)? Classify(RgbaColor pixel) =>
        pixel.A == 0 ? null : (Water, WaterFlag);

    // Overlays the water PNG onto the existing terrain in place: opaque pixels overwrite
    // waterHeight/flags for that tile; fully-transparent pixels leave the tile as-is.
    // Size mismatch is a hard error rather than a silent truncate/zero-fill — there's no safe
    // default to fabricate for tiles the image doesn't cover 1:1.
    public static void Apply(int[] waterHeight, int[] flags, IReadOnlyList<RgbaColor> pixels)
    {
        if (pixels.Count != waterHeight.Length)
        {
            throw new InvalidOperationException(
                $"Water image has {pixels.Count} pixels but the map has {waterHeight.Length} tiles; they must match exactly.");
        }

        for (int i = 0; i < pixels.Count; i++)
        {
            (int WaterHeight, int Flags)? classified = Classify(pixels[i]);
            if (classified is null)
            {
                continue;
            }

            waterHeight[i] = classified.Value.WaterHeight;
            flags[i] = classified.Value.Flags;
        }
    }
}
