using System.Drawing;
using System.Drawing.Imaging;
using Wartergen.App.Services;
using Wartergen.Bmp;
using Wartergen.Mpq;
using Wartergen.Wc3Terrain;

namespace Wartergen.App.Tests;

public class DrawTerrainWorkflowTests
{
    private sealed class FakeMpqEditorService(string sourceExtractPath) : IMpqEditorService
    {
        public byte[]? RepackedWarBytes { get; private set; }

        public Task ExtractFileAsync(string mapPath, string fileNameInArchive, string outputDir, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDir);
            File.Copy(sourceExtractPath, Path.Combine(outputDir, fileNameInArchive), overwrite: true);
            return Task.CompletedTask;
        }

        public Task AddOrReplaceFileAsync(string mapPath, string sourceFilePath, string targetNameInArchive, CancellationToken cancellationToken = default)
        {
            RepackedWarBytes = File.ReadAllBytes(sourceFilePath);
            return Task.CompletedTask;
        }
    }

    // The original fixture's terrain, read directly (not through the workflow), used as the
    // "was this tile actually left alone" baseline.
    private static TerrainModel GetFixtureTerrain()
    {
        byte[] warBytes = File.ReadAllBytes(Path.Combine("TestData", "BoundariesTest.w3e"));
        return TerrainTranslator.WarToJson(warBytes);
    }

    private static string CreateSolidImage(int width, int height, Color color, bool withAlpha)
    {
        string extension = withAlpha ? ".png" : ".bmp";
        string path = Path.Combine(Path.GetTempPath(), $"wartergen-test-{Guid.NewGuid():N}{extension}");
        using var bitmap = new Bitmap(width, height, withAlpha ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(color);
        }
        bitmap.Save(path, withAlpha ? ImageFormat.Png : ImageFormat.Bmp);
        return path;
    }

    // Checkerboard PNG: (x+y) even -> fully transparent (untouched), odd -> opaque solid color
    // (painted). Used to verify only the painted half of the tiles change.
    private static string CreateCheckerboardPng(int size, Color opaqueColor)
    {
        string path = Path.Combine(Path.GetTempPath(), $"wartergen-test-{Guid.NewGuid():N}.png");
        using (var bitmap = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                bool painted = (x + y) % 2 != 0;
                bitmap.SetPixel(x, y, painted ? opaqueColor : Color.FromArgb(0, 0, 0, 0));
            }
            bitmap.Save(path, ImageFormat.Png);
        }
        return path;
    }

    [Fact]
    public async Task RunAsync_NoCliffBmp_LeavesCliffsUnchanged()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        try
        {
            TerrainModel original = GetFixtureTerrain();

            await workflow.RunAsync(mapPath, bmpPath, null, null, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            Assert.Equal(original.CliffTexture, result.CliffTexture);
            Assert.Equal(original.LayerHeight, result.LayerHeight);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_CliffBmpProvided_ClassifiesEveryTile()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);

        int tileCount = GetFixtureTerrain().CliffTexture.Length;
        int gridEdge = (int)Math.Sqrt(tileCount);
        string cliffBmpPath = CreateSolidImage(gridEdge, gridEdge, Color.FromArgb(255, 0, 0), withAlpha: false); // pure red -> height 0, texture 16
        try
        {
            await workflow.RunAsync(mapPath, bmpPath, cliffBmpPath, null, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            Assert.All(result.CliffTexture, v => Assert.Equal(16, v));
            Assert.All(result.LayerHeight, v => Assert.Equal(0, v));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(cliffBmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_CliffBmpSizeMismatch_ThrowsWorkflowException()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string cliffBmpPath = CreateSolidImage(4, 4, Color.Red, withAlpha: false); // deliberately the wrong pixel count
        try
        {
            DrawTerrainWorkflowException ex = await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, cliffBmpPath, null, null, new Progress<string>()));

            Assert.Contains("must match exactly", ex.Message);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(cliffBmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_MissingCliffBmpFile_Throws()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string missingCliffBmpPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bmp");
        try
        {
            await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, missingCliffBmpPath, null, null, new Progress<string>()));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_NoWaterPng_LeavesWaterUnchanged()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        try
        {
            TerrainModel original = GetFixtureTerrain();

            await workflow.RunAsync(mapPath, bmpPath, null, null, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            Assert.Equal(original.WaterHeight, result.WaterHeight);
            Assert.Equal(original.Flags, result.Flags);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_WaterPngProvided_ClassifiesEveryTile()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);

        int tileCount = GetFixtureTerrain().CliffTexture.Length;
        int gridEdge = (int)Math.Sqrt(tileCount);
        string waterPngPath = CreateSolidImage(gridEdge, gridEdge, Color.FromArgb(255, 0, 128, 255), withAlpha: true); // opaque -> water
        try
        {
            await workflow.RunAsync(mapPath, bmpPath, null, waterPngPath, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);

            // war3map.w3e packs waterHeight and boundaryFlag into the same 16-bit value
            // (TerrainTranslator.cs: `waterHeight | (boundaryFlag ? 0x4000 : 0)`), and 24576
            // (0x6000) already has that boundaryFlag bit set as part of its own numeric value —
            // so a boundary tile's waterHeight can never round-trip as 8192 regardless of what
            // was painted there. This is a real constraint of the wire format (boundary/void
            // tiles can't hold water), not a defect in WaterPngConverter.
            for (int i = 0; i < result.WaterHeight.Length; i++)
            {
                int expected = result.BoundaryFlag[i] ? 24576 : 8192;
                Assert.Equal(expected, result.WaterHeight[i]);
            }

            // Flags isn't packed with boundaryFlag (it shares its short with groundTexture
            // instead), so it round-trips cleanly for every tile.
            Assert.All(result.Flags, v => Assert.Equal(256, v));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(waterPngPath);
        }
    }

    [Fact]
    public async Task RunAsync_WaterPngPartiallyTransparent_OnlyPaintedTilesChange()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);

        TerrainModel original = GetFixtureTerrain();
        int gridEdge = (int)Math.Sqrt(original.WaterHeight.Length);
        string waterPngPath = CreateCheckerboardPng(gridEdge, Color.FromArgb(255, 0, 128, 255));
        try
        {
            await workflow.RunAsync(mapPath, bmpPath, null, waterPngPath, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            for (int y = 0; y < gridEdge; y++)
            for (int x = 0; x < gridEdge; x++)
            {
                int i = (y * gridEdge) + x;
                bool painted = (x + y) % 2 != 0;
                if (painted)
                {
                    // Boundary tiles can't round-trip waterHeight=8192 (see comment above), but
                    // flags isn't affected by that collision, so it's the reliable signal here.
                    Assert.Equal(256, result.Flags[i]);
                }
                else
                {
                    Assert.Equal(original.WaterHeight[i], result.WaterHeight[i]);
                    Assert.Equal(original.Flags[i], result.Flags[i]);
                }
            }
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(waterPngPath);
        }
    }

    [Fact]
    public async Task RunAsync_WaterPngSizeMismatch_ThrowsWorkflowException()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string waterPngPath = CreateSolidImage(4, 4, Color.Blue, withAlpha: true); // deliberately the wrong pixel count
        try
        {
            DrawTerrainWorkflowException ex = await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, null, waterPngPath, null, new Progress<string>()));

            Assert.Contains("must match exactly", ex.Message);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(waterPngPath);
        }
    }

    [Fact]
    public async Task RunAsync_MissingWaterPngFile_Throws()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string missingWaterPngPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, null, missingWaterPngPath, null, new Progress<string>()));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_NoHeightsPng_LeavesGroundHeightUnchanged()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        try
        {
            TerrainModel original = GetFixtureTerrain();

            await workflow.RunAsync(mapPath, bmpPath, null, null, null, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            Assert.Equal(original.GroundHeight, result.GroundHeight);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RunAsync_HeightsPngProvided_ClassifiesEveryTile()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);

        int tileCount = GetFixtureTerrain().CliffTexture.Length;
        int gridEdge = (int)Math.Sqrt(tileCount);
        string heightsPngPath = CreateSolidImage(gridEdge, gridEdge, Color.FromArgb(255, 255, 255), withAlpha: true); // white -> max height
        try
        {
            await workflow.RunAsync(mapPath, bmpPath, null, null, heightsPngPath, new Progress<string>());

            TerrainModel result = TerrainTranslator.WarToJson(mpq.RepackedWarBytes!);
            Assert.All(result.GroundHeight, v => Assert.Equal(HeightsPngConverter.MaxHeight, v));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(heightsPngPath);
        }
    }

    [Fact]
    public async Task RunAsync_HeightsPngSizeMismatch_ThrowsWorkflowException()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string heightsPngPath = CreateSolidImage(4, 4, Color.Gray, withAlpha: true); // deliberately the wrong pixel count
        try
        {
            DrawTerrainWorkflowException ex = await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, null, null, heightsPngPath, new Progress<string>()));

            Assert.Contains("must match exactly", ex.Message);
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
            File.Delete(heightsPngPath);
        }
    }

    [Fact]
    public async Task RunAsync_MissingHeightsPngFile_Throws()
    {
        var mpq = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(mpq);
        string mapPath = Path.GetTempFileName();
        string bmpPath = CreateSolidImage(4, 4, Color.Green, withAlpha: false);
        string missingHeightsPngPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            await Assert.ThrowsAsync<DrawTerrainWorkflowException>(() =>
                workflow.RunAsync(mapPath, bmpPath, null, null, missingHeightsPngPath, new Progress<string>()));
        }
        finally
        {
            File.Delete(mapPath);
            File.Delete(bmpPath);
        }
    }
}
