namespace Wartergen.Bmp;

// Overlays a user-painted grayscale heightmap PNG (read via PngPixelReader) onto war3map.w3e's
// groundHeight array, in place, corner by corner. Unlike WaterPngConverter, alpha is ignored
// entirely — every pixel always produces a height value; there is no "skip this corner" case.
// Grayscale is read from the red channel only (R=G=B is assumed for a true grayscale PNG),
// matching the existing converters' simplicity.
public static class HeightsPngConverter
{
    public const int MinHeight = 7680;
    public const int MaxHeight = 16383;

    // Typical flat-map height. Not a formula anchor — Classify is a plain linear map from
    // MinHeight to MaxHeight — just a reference point for the UI legend.
    public const int DefaultHeight = 8192;

    // Linear map: black (0) -> MinHeight, white (255) -> MaxHeight. Rounded to the nearest int
    // since groundHeight is stored as a whole-number short in war3map.w3e.
    public static int Classify(RgbaColor pixel) =>
        (int)Math.Round(MinHeight + (pixel.R / 255.0) * (MaxHeight - MinHeight));

    // Overlays the heights PNG onto the existing terrain in place: every pixel overwrites its
    // corner's groundHeight, no exceptions. Size mismatch is a hard error rather than a silent
    // truncate/zero-fill — there's no safe default to fabricate for corners the image doesn't
    // cover 1:1.
    public static void Apply(int[] groundHeight, IReadOnlyList<RgbaColor> pixels)
    {
        if (pixels.Count != groundHeight.Length)
        {
            throw new InvalidOperationException(
                $"Heights image has {pixels.Count} pixels but the map has {groundHeight.Length} corners; they must match exactly.");
        }

        for (int i = 0; i < pixels.Count; i++)
        {
            groundHeight[i] = Classify(pixels[i]);
        }
    }
}
