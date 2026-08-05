using System.Drawing;
using System.Drawing.Imaging;
using Wartergen.App.Services;
using Wartergen.App.ViewModels;
using Wartergen.Mpq;

namespace Wartergen.App.Tests;

public class MainViewModelBmpPreviewTests
{
    private sealed class FakeMpqEditorService(string sourceFilePath) : IMpqEditorService
    {
        public Task ExtractFileAsync(string mapPath, string fileNameInArchive, string outputDir, CancellationToken cancellationToken = default)
        {
            Directory.CreateDirectory(outputDir);
            File.Copy(sourceFilePath, Path.Combine(outputDir, fileNameInArchive), overwrite: true);
            return Task.CompletedTask;
        }

        public Task AddOrReplaceFileAsync(string mapPath, string sourceFilePath, string targetNameInArchive, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by the BMP preview feature.");
    }

    private static MainViewModel CreateViewModel()
    {
        var fake = new FakeMpqEditorService(Path.Combine("TestData", "BoundariesTest.w3e"));
        var workflow = new DrawTerrainWorkflow(fake);
        return new MainViewModel(workflow);
    }

    private static string CreateSmallBmp(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bmp");
        using var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Green);
        }
        bitmap.Save(path, ImageFormat.Bmp);
        return path;
    }

    private static string CreateSmallPng(int width, int height)
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Red);
        }
        bitmap.Save(path, ImageFormat.Png);
        return path;
    }

    [Fact]
    public async Task RefreshBmpPreviewAsync_SmallBmp_ScalesToNativeSizeAndPositionsOverlay()
    {
        var viewModel = CreateViewModel();
        string bmpPath = CreateSmallBmp(100, 50);
        try
        {
            viewModel.BmpFilePath = bmpPath;
            await viewModel.RefreshBmpPreviewAsync();

            Assert.NotNull(viewModel.BmpPreviewSource);
            Assert.Equal(100, viewModel.PreviewWidth, precision: 3);
            Assert.Equal(50, viewModel.PreviewHeight, precision: 3);
            Assert.Equal(string.Empty, viewModel.PreviewError);
            Assert.True(viewModel.BorderWidth > 0);
            Assert.True(viewModel.BorderHeight > 0);

            // Center crosshair and border rectangle must land inside the displayed image bounds.
            Assert.InRange(viewModel.CrosshairX, 0, viewModel.PreviewWidth);
            Assert.InRange(viewModel.CrosshairY, 0, viewModel.PreviewHeight);
            Assert.InRange(viewModel.BorderLeft, 0, viewModel.PreviewWidth);
            Assert.InRange(viewModel.BorderTop, 0, viewModel.PreviewHeight);
        }
        finally
        {
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RefreshBmpPreviewAsync_LargeBmp_ScalesDownToMaxDimension()
    {
        var viewModel = CreateViewModel();
        string bmpPath = CreateSmallBmp(1000, 500); // 2:1 aspect ratio, exceeds the 481px cap
        try
        {
            viewModel.BmpFilePath = bmpPath;
            await viewModel.RefreshBmpPreviewAsync();

            Assert.Equal(481, viewModel.PreviewWidth, precision: 3);
            Assert.Equal(240.5, viewModel.PreviewHeight, precision: 3);
        }
        finally
        {
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public async Task RefreshBmpPreviewAsync_MissingFile_SetsErrorAndClearsPreview()
    {
        var viewModel = CreateViewModel();

        viewModel.BmpFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.bmp");
        await viewModel.RefreshBmpPreviewAsync();

        Assert.Null(viewModel.BmpPreviewSource);
        Assert.StartsWith("ERROR:", viewModel.PreviewError);
    }

    [Fact]
    public async Task RefreshBmpPreviewAsync_EmptyPath_ClearsPreviewWithoutError()
    {
        var viewModel = CreateViewModel();
        string bmpPath = CreateSmallBmp(20, 20);
        try
        {
            viewModel.BmpFilePath = bmpPath;
            await viewModel.RefreshBmpPreviewAsync();
            Assert.NotNull(viewModel.BmpPreviewSource);

            viewModel.BmpFilePath = string.Empty;
            await viewModel.RefreshBmpPreviewAsync();

            Assert.Null(viewModel.BmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.PreviewError);
        }
        finally
        {
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public void ZoomPercent_DefaultsToOneHundred()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(100, viewModel.ZoomPercent);
    }

    [Fact]
    public async Task ChangingZoomPercent_RescalesPreviewAndOverlayProportionally()
    {
        var viewModel = CreateViewModel();
        string bmpPath = CreateSmallBmp(100, 50);
        try
        {
            viewModel.BmpFilePath = bmpPath;
            await viewModel.RefreshBmpPreviewAsync();

            double baseWidth = viewModel.PreviewWidth;
            double baseCrosshairX = viewModel.CrosshairX;
            double baseBorderLeft = viewModel.BorderLeft;

            viewModel.ZoomPercent = 200;

            Assert.Equal(baseWidth * 2, viewModel.PreviewWidth, precision: 3);
            Assert.Equal(baseCrosshairX * 2, viewModel.CrosshairX, precision: 3);
            Assert.Equal(baseBorderLeft * 2, viewModel.BorderLeft, precision: 3);
        }
        finally
        {
            File.Delete(bmpPath);
        }
    }

    [Fact]
    public void ShowCrosshair_DefaultsToTrue()
    {
        var viewModel = CreateViewModel();

        Assert.True(viewModel.ShowCrosshair);
    }

    [Fact]
    public void CanExecuteDrawTerrain_IgnoresCliffAndWaterFilePaths()
    {
        var viewModel = CreateViewModel();
        viewModel.MapFilePath = "map.w3x";
        viewModel.BmpFilePath = "terrain.bmp";

        Assert.True(viewModel.DrawTerrainCommand.CanExecute(null));

        viewModel.CliffBmpFilePath = string.Empty;
        viewModel.WaterPngFilePath = string.Empty;
        viewModel.HeightsPngFilePath = string.Empty;

        Assert.True(viewModel.DrawTerrainCommand.CanExecute(null));
    }

    [Fact]
    public void CliffOpacityPercent_DefaultsToFifty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(50, viewModel.CliffOpacityPercent);
        Assert.Equal(0.5, viewModel.CliffOpacityFraction);
    }

    [Fact]
    public void CliffOpacityFraction_TracksCliffOpacityPercent()
    {
        var viewModel = CreateViewModel();

        viewModel.CliffOpacityPercent = 25;

        Assert.Equal(0.25, viewModel.CliffOpacityFraction);
    }

    [Fact]
    public void CliffOpacityFraction_ZeroWhenCliffDisabled()
    {
        var viewModel = CreateViewModel();

        viewModel.CliffOpacityPercent = 80;
        viewModel.CliffEnabled = false;

        Assert.Equal(0.0, viewModel.CliffOpacityFraction);

        viewModel.CliffEnabled = true;

        Assert.Equal(0.8, viewModel.CliffOpacityFraction);
    }

    [Fact]
    public async Task RefreshCliffPreviewAsync_LoadsPng()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.CliffBmpFilePath = pngPath;
            await viewModel.RefreshCliffPreviewAsync();

            Assert.NotNull(viewModel.CliffBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.CliffPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public async Task RefreshCliffPreviewAsync_MissingFile_SetsErrorAndClearsPreview()
    {
        var viewModel = CreateViewModel();

        viewModel.CliffBmpFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await viewModel.RefreshCliffPreviewAsync();

        Assert.Null(viewModel.CliffBmpPreviewSource);
        Assert.StartsWith("ERROR:", viewModel.CliffPreviewError);
    }

    [Fact]
    public async Task RefreshCliffPreviewAsync_EmptyPath_ClearsPreviewWithoutError()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.CliffBmpFilePath = pngPath;
            await viewModel.RefreshCliffPreviewAsync();
            Assert.NotNull(viewModel.CliffBmpPreviewSource);

            viewModel.CliffBmpFilePath = string.Empty;
            await viewModel.RefreshCliffPreviewAsync();

            Assert.Null(viewModel.CliffBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.CliffPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void WaterOpacityPercent_DefaultsToFifty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(50, viewModel.WaterOpacityPercent);
        Assert.Equal(0.5, viewModel.WaterOpacityFraction);
    }

    [Fact]
    public void WaterOpacityFraction_TracksWaterOpacityPercent()
    {
        var viewModel = CreateViewModel();

        viewModel.WaterOpacityPercent = 25;

        Assert.Equal(0.25, viewModel.WaterOpacityFraction);
    }

    [Fact]
    public void WaterOpacityFraction_ZeroWhenWaterDisabled()
    {
        var viewModel = CreateViewModel();

        viewModel.WaterOpacityPercent = 80;
        viewModel.WaterEnabled = false;

        Assert.Equal(0.0, viewModel.WaterOpacityFraction);

        viewModel.WaterEnabled = true;

        Assert.Equal(0.8, viewModel.WaterOpacityFraction);
    }

    [Fact]
    public async Task RefreshWaterPreviewAsync_LoadsPng()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.WaterPngFilePath = pngPath;
            await viewModel.RefreshWaterPreviewAsync();

            Assert.NotNull(viewModel.WaterBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.WaterPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public async Task RefreshWaterPreviewAsync_MissingFile_SetsErrorAndClearsPreview()
    {
        var viewModel = CreateViewModel();

        viewModel.WaterPngFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await viewModel.RefreshWaterPreviewAsync();

        Assert.Null(viewModel.WaterBmpPreviewSource);
        Assert.StartsWith("ERROR:", viewModel.WaterPreviewError);
    }

    [Fact]
    public async Task RefreshWaterPreviewAsync_EmptyPath_ClearsPreviewWithoutError()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.WaterPngFilePath = pngPath;
            await viewModel.RefreshWaterPreviewAsync();
            Assert.NotNull(viewModel.WaterBmpPreviewSource);

            viewModel.WaterPngFilePath = string.Empty;
            await viewModel.RefreshWaterPreviewAsync();

            Assert.Null(viewModel.WaterBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.WaterPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void HeightsOpacityPercent_DefaultsToFifty()
    {
        var viewModel = CreateViewModel();

        Assert.Equal(50, viewModel.HeightsOpacityPercent);
        Assert.Equal(0.5, viewModel.HeightsOpacityFraction);
    }

    [Fact]
    public void HeightsOpacityFraction_TracksHeightsOpacityPercent()
    {
        var viewModel = CreateViewModel();

        viewModel.HeightsOpacityPercent = 25;

        Assert.Equal(0.25, viewModel.HeightsOpacityFraction);
    }

    [Fact]
    public void HeightsOpacityFraction_ZeroWhenHeightsDisabled()
    {
        var viewModel = CreateViewModel();

        viewModel.HeightsOpacityPercent = 80;
        viewModel.HeightsEnabled = false;

        Assert.Equal(0.0, viewModel.HeightsOpacityFraction);

        viewModel.HeightsEnabled = true;

        Assert.Equal(0.8, viewModel.HeightsOpacityFraction);
    }

    [Fact]
    public async Task RefreshHeightsPreviewAsync_LoadsPng()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.HeightsPngFilePath = pngPath;
            await viewModel.RefreshHeightsPreviewAsync();

            Assert.NotNull(viewModel.HeightsBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.HeightsPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public async Task RefreshHeightsPreviewAsync_MissingFile_SetsErrorAndClearsPreview()
    {
        var viewModel = CreateViewModel();

        viewModel.HeightsPngFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        await viewModel.RefreshHeightsPreviewAsync();

        Assert.Null(viewModel.HeightsBmpPreviewSource);
        Assert.StartsWith("ERROR:", viewModel.HeightsPreviewError);
    }

    [Fact]
    public async Task RefreshHeightsPreviewAsync_EmptyPath_ClearsPreviewWithoutError()
    {
        var viewModel = CreateViewModel();
        string pngPath = CreateSmallPng(20, 20);
        try
        {
            viewModel.HeightsPngFilePath = pngPath;
            await viewModel.RefreshHeightsPreviewAsync();
            Assert.NotNull(viewModel.HeightsBmpPreviewSource);

            viewModel.HeightsPngFilePath = string.Empty;
            await viewModel.RefreshHeightsPreviewAsync();

            Assert.Null(viewModel.HeightsBmpPreviewSource);
            Assert.Equal(string.Empty, viewModel.HeightsPreviewError);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }
}
