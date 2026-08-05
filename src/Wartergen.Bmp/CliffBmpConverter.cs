using System.Drawing;

namespace Wartergen.Bmp;

// Overlays a user-painted cliff BMP onto war3map.w3e's cliffTexture/layerHeight arrays, in place,
// tile by tile. Per pixel: hue (in rainbow/ROYGBIV order) selects layerHeight (0-14) and
// brightness (max channel) picks between the two valid cliff textures, 16 (bright) or 0 (dark).
// Saturation is not read — painted hues are expected to be fully saturated. Every pixel is
// authoritative (unlike the old PNG format, cliff BMPs have no alpha/"leave tile untouched"
// escape hatch — every tile the image covers must be explicitly painted).
//
// Heights 0-5 are the levels actually used by most maps, so they get a wide slice of the hue
// circle (300 degrees, 50 each) for maximum visual separation ("vivid"); heights 6-14 are rare
// and share a narrow slice (the remaining 60 degrees, ~6.67 each), so they look like subtly
// different shades of the same hue rather than competing for visual distinctness with 0-5.
//
// After classification, a border-correction pass reclassifies any tile that touches a
// differently-heighted neighbor (8-neighbor adjacency) to whichever side's border is "stronger" —
// see ResolveBorders for the exact rule, reverse-engineered from samples/cliffdebug.png and
// samples/cliffalgorithm.png. Only cliffTexture is ever reassigned by that pass — layerHeight is
// real terrain geometry (the actual cliff drop) and is never touched, only ever read to decide
// where a cliff edge is.
public static class CliffBmpConverter
{
    public const int HeightCount = 15; // layerHeight 0-14

    private const int BrightCliffTexture = 16;
    private const int DarkCliffTexture = 0;
    private const double BrightnessThreshold = 0.5;

    private const int VividHeightCount = 6; // heights 0-5
    private const int SubtleHeightCount = HeightCount - VividHeightCount; // heights 6-14
    private const double VividArcDegrees = 300.0;
    private const double SubtleArcDegrees = 360.0 - VividArcDegrees;
    private const double VividBucketDegrees = VividArcDegrees / VividHeightCount;
    private const double SubtleBucketDegrees = SubtleArcDegrees / SubtleHeightCount;

    private static readonly int[] NeighborDx = [-1, 0, 1, -1, 1, -1, 0, 1];
    private static readonly int[] NeighborDy = [-1, -1, -1, 0, 0, 1, 1, 1];

    // Classifies a single pixel into (cliffTexture, layerHeight).
    public static (int CliffTexture, int LayerHeight) Classify(RgbColor pixel)
    {
        double hue = Color.FromArgb(pixel.R, pixel.G, pixel.B).GetHue();
        int layerHeight = HueToHeight(hue);

        double brightness = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B)) / 255.0;
        int cliffTexture = brightness >= BrightnessThreshold ? BrightCliffTexture : DarkCliffTexture;

        return (cliffTexture, layerHeight);
    }

    // Overlays the cliff BMP onto the existing terrain in place: every pixel classifies and
    // (after border correction) overwrites its tile's cliffTexture/layerHeight. Size mismatch is
    // a hard error rather than a silent truncate/zero-fill — there's no safe default to fabricate
    // for tiles the image doesn't cover 1:1. `width` is the map's tile-grid width (pixels are
    // row-major, top-left first, matching PngPixelReader/BmpGroundTextureConverter), needed to
    // reconstruct (x, y) for neighbor lookups during border correction.
    public static void Apply(int[] cliffTexture, int[] layerHeight, IReadOnlyList<RgbColor> pixels, int width)
    {
        if (pixels.Count != cliffTexture.Length)
        {
            throw new InvalidOperationException(
                $"Cliff image has {pixels.Count} pixels but the map has {cliffTexture.Length} tiles; they must match exactly.");
        }

        var types = new (int CliffTexture, int LayerHeight)[pixels.Count];
        for (int i = 0; i < pixels.Count; i++)
        {
            types[i] = Classify(pixels[i]);
        }

        (int CliffTexture, int LayerHeight)[] resolved = ResolveBorders(types, width);

        for (int i = 0; i < resolved.Length; i++)
        {
            cliffTexture[i] = resolved[i].CliffTexture;
            layerHeight[i] = resolved[i].LayerHeight;
        }
    }

    // A cliff edge is defined by a real layerHeight discontinuity — regardless of what
    // cliffTexture happens to be painted on either side, since cliffTexture is just the cosmetic
    // face-texture choice (it can and often should vary along a cliff, e.g. alternating
    // bright/dark tiles for visual variety) and carries no geometry. Where two differently-heighted
    // regions touch (8-neighbor adjacency), the 1-tile-wide ring on the losing height's side gets
    // its cliffTexture reassigned — never its layerHeight, which is preserved exactly as painted
    // for every tile, always.
    //
    // For a touching pair of heights (A, B), "A's ring" is the set of height-A pixels with at
    // least one height-B neighbor; whichever ring has MORE pixels wins — not which height covers
    // more total area — so a large but smooth region can still lose to a smaller, jaggier one
    // along their shared edge. Ties leave both sides unconverted (no sample evidence either way,
    // so default to a no-op). A losing pixel's new cliffTexture is the majority texture among its
    // own neighbors that have the winning height, so any alternating texture pattern already
    // present on the winning side carries over instead of being flattened to one fixed value.
    //
    // This resolves independently per touching height-pair from the same starting classification
    // (a single pass, not an iterative re-convergence), so it naturally recurses to any nesting
    // depth: an outer boundary and a deeply-nested inner boundary are each judged on their own
    // ring sizes, with no special-cased "background" height. A pixel touching more than one
    // different height (and losing to more than one of them) converts to whichever losing
    // neighbor height has the largest ring-size margin — an edge case with no sample coverage,
    // resolved this way for a deterministic, explainable result.
    private static (int CliffTexture, int LayerHeight)[] ResolveBorders(
        (int CliffTexture, int LayerHeight)[] types, int width)
    {
        int height = types.Length / width;
        var neighborHeightsByIndex = new List<int>[types.Length];
        var ringCounts = new Dictionary<(int OwnHeight, int NeighborHeight), int>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                int ownHeight = types[index].LayerHeight;
                List<int> neighborHeights = DistinctOtherNeighborHeights(types, width, height, x, y, ownHeight);
                neighborHeightsByIndex[index] = neighborHeights;

                foreach (int neighborHeight in neighborHeights)
                {
                    var key = (ownHeight, neighborHeight);
                    ringCounts[key] = ringCounts.GetValueOrDefault(key) + 1;
                }
            }
        }

        var result = new (int CliffTexture, int LayerHeight)[types.Length];
        Array.Copy(types, result, types.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                int ownHeight = types[index].LayerHeight;
                int? bestLoserHeight = null;
                int bestMargin = 0;

                foreach (int neighborHeight in neighborHeightsByIndex[index])
                {
                    int ownRing = ringCounts[(ownHeight, neighborHeight)];
                    int neighborRing = ringCounts[(neighborHeight, ownHeight)];
                    if (neighborRing <= ownRing)
                    {
                        continue; // own height wins or ties against this neighbor height
                    }

                    int margin = neighborRing - ownRing;
                    if (bestLoserHeight is null || margin > bestMargin)
                    {
                        bestLoserHeight = neighborHeight;
                        bestMargin = margin;
                    }
                }

                if (bestLoserHeight is not null)
                {
                    int winningTexture = MajorityNeighborTexture(types, width, height, x, y, bestLoserHeight.Value);
                    result[index] = (winningTexture, ownHeight);
                }
            }
        }

        return result;
    }

    private static List<int> DistinctOtherNeighborHeights(
        (int CliffTexture, int LayerHeight)[] types, int width, int height, int x, int y, int ownHeight)
    {
        var distinct = new List<int>();

        for (int i = 0; i < NeighborDx.Length; i++)
        {
            int nx = x + NeighborDx[i];
            int ny = y + NeighborDy[i];
            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            {
                continue;
            }

            int neighborHeight = types[(ny * width) + nx].LayerHeight;
            if (neighborHeight != ownHeight && !distinct.Contains(neighborHeight))
            {
                distinct.Add(neighborHeight);
            }
        }

        return distinct;
    }

    // Among (x, y)'s actual 8 neighbors that have the given (winning) layerHeight, the most
    // common cliffTexture — ties broken by the smaller texture value for determinism.
    private static int MajorityNeighborTexture(
        (int CliffTexture, int LayerHeight)[] types, int width, int height, int x, int y, int winningHeight)
    {
        var votes = new Dictionary<int, int>();

        for (int i = 0; i < NeighborDx.Length; i++)
        {
            int nx = x + NeighborDx[i];
            int ny = y + NeighborDy[i];
            if (nx < 0 || nx >= width || ny < 0 || ny >= height)
            {
                continue;
            }

            (int CliffTexture, int LayerHeight) neighbor = types[(ny * width) + nx];
            if (neighbor.LayerHeight == winningHeight)
            {
                votes[neighbor.CliffTexture] = votes.GetValueOrDefault(neighbor.CliffTexture) + 1;
            }
        }

        return votes
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .First().Key;
    }

    // The single source of truth for height <-> hue, shared with CliffColorLegendBuilder (the
    // App-layer legend UI) so the swatches it displays are guaranteed to decode back to the
    // height they claim to represent.
    public static double GetCanonicalHeightHue(int height)
    {
        if (height < 0 || height >= HeightCount)
        {
            throw new ArgumentOutOfRangeException(nameof(height), height, $"Height must be between 0 and {HeightCount - 1}.");
        }

        return BucketStart(height) + (BucketWidth(height) / 2.0);
    }

    private static int HueToHeight(double hueDegrees)
    {
        if (hueDegrees < VividArcDegrees)
        {
            return Math.Clamp((int)(hueDegrees / VividBucketDegrees), 0, VividHeightCount - 1);
        }

        int subtleIndex = (int)((hueDegrees - VividArcDegrees) / SubtleBucketDegrees);
        return VividHeightCount + Math.Clamp(subtleIndex, 0, SubtleHeightCount - 1);
    }

    private static double BucketStart(int height) =>
        height < VividHeightCount
            ? height * VividBucketDegrees
            : VividArcDegrees + ((height - VividHeightCount) * SubtleBucketDegrees);

    private static double BucketWidth(int height) => height < VividHeightCount ? VividBucketDegrees : SubtleBucketDegrees;
}
