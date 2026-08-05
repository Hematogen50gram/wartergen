using Wartergen.Bmp;

namespace Wartergen.Bmp.Tests;

public class CliffBmpConverterTests
{
    [Fact]
    public void Classify_PureRed_IsLowestHeightAndBright()
    {
        var pixel = new RgbColor(255, 0, 0);

        (int cliffTexture, int layerHeight) classified = CliffBmpConverter.Classify(pixel);

        Assert.Equal(0, classified.layerHeight);
        Assert.Equal(16, classified.cliffTexture);
    }

    [Fact]
    public void Classify_Cyan_IsVividHeightThree()
    {
        // Heights 0-5 ("vivid") each get a 50-degree slice of the hue circle: hue 180 falls in
        // [150, 200), the slice for height 3 (180 / 50 = 3.6, truncated to 3).
        var pixel = new RgbColor(0, 255, 255);

        (int _, int layerHeight) classified = CliffBmpConverter.Classify(pixel);

        Assert.Equal(3, classified.layerHeight);
    }

    [Fact]
    public void Classify_NearFullCircleHue_IsHighestSubtleHeight()
    {
        // R=255,G=0,B=9 -> hue ~357.9 degrees. Heights 6-14 ("subtle") share the remaining
        // 60-degree slice (~6.67 degrees each) starting at 300 degrees; 357.9 falls in the last
        // of those 9 slices, height 14.
        var pixel = new RgbColor(255, 0, 9);

        (int _, int layerHeight) classified = CliffBmpConverter.Classify(pixel);

        Assert.Equal(14, classified.layerHeight);
    }

    [Fact]
    public void GetCanonicalHeightHue_VividHeightsAreWidelySeparated()
    {
        double[] vividHues = Enumerable.Range(0, 6).Select(CliffBmpConverter.GetCanonicalHeightHue).ToArray();

        for (int i = 1; i < vividHues.Length; i++)
        {
            Assert.Equal(50.0, vividHues[i] - vividHues[i - 1], precision: 3);
        }
    }

    [Fact]
    public void GetCanonicalHeightHue_SubtleHeightsAreClosePacked()
    {
        double[] subtleHues = Enumerable.Range(6, 9).Select(CliffBmpConverter.GetCanonicalHeightHue).ToArray();

        Assert.True(subtleHues[0] >= 300.0);
        Assert.True(subtleHues[^1] < 360.0);
        for (int i = 1; i < subtleHues.Length; i++)
        {
            Assert.True(subtleHues[i] - subtleHues[i - 1] < 10.0);
        }
    }

    [Fact]
    public void Classify_SameHueDarkerValue_IsDarkTexture()
    {
        var pixel = new RgbColor(100, 0, 0); // hue 0, value ~0.39 -> dark

        (int cliffTexture, int _) classified = CliffBmpConverter.Classify(pixel);

        Assert.Equal(0, classified.cliffTexture);
    }

    [Fact]
    public void Apply_EveryPixelOverwritesItsTile()
    {
        int[] cliffTexture = [240, 240];
        int[] layerHeight = [2, 2];
        RgbColor[] pixels = [new RgbColor(255, 0, 0), new RgbColor(100, 0, 0)]; // height 0/bright, height 0/dark

        CliffBmpConverter.Apply(cliffTexture, layerHeight, pixels, width: 2);

        Assert.Equal([16, 0], cliffTexture);
        Assert.Equal([0, 0], layerHeight);
    }

    [Fact]
    public void Apply_MismatchedLength_Throws()
    {
        int[] cliffTexture = [240, 240, 240];
        int[] layerHeight = [2, 2, 2];
        RgbColor[] pixels = [new RgbColor(255, 0, 0), new RgbColor(0, 0, 0)];

        Assert.Throws<InvalidOperationException>(() =>
            CliffBmpConverter.Apply(cliffTexture, layerHeight, pixels, width: 3));
    }

    // A single stray pixel of a different HEIGHT, fully surrounded by one uniform height, has a
    // border ring of just itself (1 pixel) against the surrounding height's ring of all 8
    // neighbors — it loses and its cliffTexture is reassigned to the surrounding type's texture.
    // Its own layerHeight is never touched — only real terrain geometry-free cliffTexture moves.
    [Fact]
    public void Apply_LoneDifferentHeightPixel_TextureConvertsButHeightIsPreserved()
    {
        // 3x3 grid: a red (bright, height 0) ring around a single blue (hue ~240 -> height 4,
        // dark since value ~0.39) center pixel.
        var outer = new RgbColor(255, 0, 0);
        var center = new RgbColor(0, 0, 100);
        RgbColor[] pixels =
        [
            outer, outer, outer,
            outer, center, outer,
            outer, outer, outer,
        ];
        int[] cliffTexture = new int[9];
        int[] layerHeight = new int[9];

        CliffBmpConverter.Apply(cliffTexture, layerHeight, pixels, width: 3);

        (int CliffTexture, int LayerHeight) outerType = CliffBmpConverter.Classify(outer);
        (int CliffTexture, int LayerHeight) centerType = CliffBmpConverter.Classify(center);
        for (int i = 0; i < 9; i++)
        {
            if (i == 4)
            {
                // Center: texture adopts the surrounding (winning) type's texture, but its own
                // layerHeight is preserved exactly — it does NOT become the outer type's height.
                Assert.Equal(outerType.CliffTexture, cliffTexture[i]);
                Assert.Equal(centerType.LayerHeight, layerHeight[i]);
            }
            else
            {
                Assert.Equal(outerType.CliffTexture, cliffTexture[i]);
                Assert.Equal(outerType.LayerHeight, layerHeight[i]);
            }
        }
    }

    // A 5x5 grid: a uniform-height outer ring (16 pixels) around a 3x3 block of a different
    // height. The block's own 8 perimeter pixels each touch the (larger) outer ring and lose,
    // adopting the outer ring's cliffTexture while keeping their own layerHeight; the single
    // truly-interior pixel at the block's center never touches the outer ring at all, so it's
    // fully untouched — the border pass only ever eats the outermost layer of a losing region's
    // texture, never its interior, and never any tile's height.
    [Fact]
    public void Apply_InteriorPixelNotTouchingBoundary_IsUntouched()
    {
        var outer = new RgbColor(255, 0, 0);
        var block = new RgbColor(0, 0, 100);
        const int width = 5;
        RgbColor[] pixels = new RgbColor[width * width];
        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool isBlock = x >= 1 && x <= 3 && y >= 1 && y <= 3;
                pixels[(y * width) + x] = isBlock ? block : outer;
            }
        }

        int[] cliffTexture = new int[pixels.Length];
        int[] layerHeight = new int[pixels.Length];
        CliffBmpConverter.Apply(cliffTexture, layerHeight, pixels, width);

        (int CliffTexture, int LayerHeight) outerType = CliffBmpConverter.Classify(outer);
        (int CliffTexture, int LayerHeight) blockType = CliffBmpConverter.Classify(block);

        int centerIndex = (2 * width) + 2;
        Assert.Equal(blockType.CliffTexture, cliffTexture[centerIndex]);
        Assert.Equal(blockType.LayerHeight, layerHeight[centerIndex]);

        for (int y = 0; y < width; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width) + x;
                if (index == centerIndex)
                {
                    continue;
                }

                bool isBlockPerimeter = x >= 1 && x <= 3 && y >= 1 && y <= 3;
                if (isBlockPerimeter)
                {
                    // Block perimeter: texture flips to the outer ring's texture, height stays
                    // the block's own (never overwritten to the outer ring's height).
                    Assert.Equal(outerType.CliffTexture, cliffTexture[index]);
                    Assert.Equal(blockType.LayerHeight, layerHeight[index]);
                }
                else
                {
                    Assert.Equal(outerType.CliffTexture, cliffTexture[index]);
                    Assert.Equal(outerType.LayerHeight, layerHeight[index]);
                }
            }
        }
    }

    // Oracle test against samples/cliffdebug.png (input) and samples/cliffalgorithm.png (the
    // hand-drawn answer key for the outer boundary), copied into TestData/. Reproduces the exact
    // ring-count-based border rule reverse-engineered from those two images: brown's border ring
    // (touching green) beats green's border ring (touching brown), so outer green pixels adopt
    // brown's texture; the same rule recurses into the nested cyan-inside-green boundary — cyan's
    // ring touching green (38px) loses to green's ring touching cyan (46px), so cyan's outer
    // pixels adopt green's texture, while cyan's deep interior (56px, never touching green) is
    // untouched. Every tile keeps its own original layerHeight regardless — only cliffTexture is
    // ever reassigned by border correction, since layerHeight is real terrain geometry. The
    // reference PNG only illustrates the outer (green/brown) boundary; the nested case is
    // asserted here against hand-verified coordinates instead, since cliffalgorithm.png doesn't
    // depict it.
    [Fact]
    public void Apply_CliffDebugSample_MatchesHandVerifiedBorderResolution()
    {
        string path = Path.Combine("TestData", "cliffdebug.png");
        RgbaColor[] rgbaPixels = PngPixelReader.ReadPixelsTopLeftFirst(path);
        RgbColor[] pixels = rgbaPixels.Select(p => new RgbColor(p.R, p.G, p.B)).ToArray();
        const int width = 65;

        int[] cliffTexture = new int[pixels.Length];
        int[] layerHeight = new int[pixels.Length];
        CliffBmpConverter.Apply(cliffTexture, layerHeight, pixels, width);

        var brown = new RgbColor(89, 37, 0);
        var green = new RgbColor(0, 255, 21);
        var cyan = new RgbColor(0, 255, 234);
        (int CliffTexture, int LayerHeight) brownType = CliffBmpConverter.Classify(brown);
        (int CliffTexture, int LayerHeight) greenType = CliffBmpConverter.Classify(green);
        (int CliffTexture, int LayerHeight) cyanType = CliffBmpConverter.Classify(cyan);

        int Index(int x, int y) => (y * width) + x;

        // Deep-interior brown corner: untouched.
        AssertTile(Index(0, 0), brownType);

        // Outer green ring touching brown directly: adopts brown's texture, keeps green's height.
        AssertTile(Index(31, 21), (brownType.CliffTexture, greenType.LayerHeight));

        // Deep-interior green (not touching brown or cyan): untouched.
        AssertTile(Index(32, 22), greenType);

        // Cyan's outer ring touching green (the nested/recursive case): adopts green's texture
        // (already the same value, 16), keeps cyan's own height (3, not green's 2).
        AssertTile(Index(29, 28), (greenType.CliffTexture, cyanType.LayerHeight));

        // Cyan's deep interior (never touches green): untouched.
        AssertTile(Index(30, 29), cyanType);

        void AssertTile(int index, (int CliffTexture, int LayerHeight) expected)
        {
            Assert.Equal(expected.CliffTexture, cliffTexture[index]);
            Assert.Equal(expected.LayerHeight, layerHeight[index]);
        }
    }
}
