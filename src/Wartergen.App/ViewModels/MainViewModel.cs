using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wartergen.App.Models;
using Wartergen.App.Services;
using Wartergen.Mpq;

namespace Wartergen.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int MaxPreviewDimension = 481;
    private const double MinZoomPercent = 100;
    private const double MaxZoomPercent = 500;

    // Playable-area border thickness, in BMP pixels, on each side. Constant for any map size.
    private const double PlayableAreaLeftInset = 7;
    private const double PlayableAreaTopInset = 9;
    private const double PlayableAreaRightInset = 7;
    private const double PlayableAreaBottomInset = 5;

    private readonly DrawTerrainWorkflow _workflow;
    private int previewRequestToken;
    private int cliffPreviewRequestToken;
    private int waterPreviewRequestToken;
    private BitmapImage? loadedBitmap;
    private BitmapImage? loadedCliffBitmap;
    private BitmapImage? loadedWaterBitmap;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string mapFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string bmpFilePath = string.Empty;

    // Optional — Draw Terrain works without it (cliffs reset to flat ground level).
    [ObservableProperty]
    private string cliffBmpFilePath = string.Empty;

    // Optional — Draw Terrain works without it (water resets to no water).
    [ObservableProperty]
    private string waterPngFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private bool isBusy;

    [ObservableProperty]
    private ImageSource? bmpPreviewSource;

    [ObservableProperty]
    private ImageSource? cliffBmpPreviewSource;

    [ObservableProperty]
    private ImageSource? waterBmpPreviewSource;

    [ObservableProperty]
    private double previewWidth;

    [ObservableProperty]
    private double previewHeight;

    [ObservableProperty]
    private double borderLeft;

    [ObservableProperty]
    private double borderTop;

    [ObservableProperty]
    private double borderWidth;

    [ObservableProperty]
    private double borderHeight;

    [ObservableProperty]
    private double crosshairX;

    [ObservableProperty]
    private double crosshairY;

    [ObservableProperty]
    private bool showCrosshair = true;

    [ObservableProperty]
    private bool showPlayableBorder = true;

    [ObservableProperty]
    private double zoomPercent = MinZoomPercent;

    [ObservableProperty]
    private double cliffOpacityPercent = 50;

    [ObservableProperty]
    private double waterOpacityPercent = 50;

    [ObservableProperty]
    private string previewError = string.Empty;

    [ObservableProperty]
    private string cliffPreviewError = string.Empty;

    [ObservableProperty]
    private string waterPreviewError = string.Empty;

    // WPF's Image.Opacity wants 0.0-1.0; the slider/label use 0-100 for consistency with ZoomPercent.
    public double CliffOpacityFraction => CliffOpacityPercent / 100.0;
    public double WaterOpacityFraction => WaterOpacityPercent / 100.0;

    partial void OnCliffOpacityPercentChanged(double value) => OnPropertyChanged(nameof(CliffOpacityFraction));
    partial void OnWaterOpacityPercentChanged(double value) => OnPropertyChanged(nameof(WaterOpacityFraction));

    public ObservableCollection<string> LogEntries { get; } = new();

    // Reference swatches for the cliff PNG's color encoding — constant, computed once.
    public IReadOnlyList<CliffColorLegendEntry> CliffColorLegend { get; } = CliffColorLegendBuilder.Build();

    public MainViewModel()
        : this(new DrawTerrainWorkflow(MpqEditorService.CreateDefault()))
    {
    }

    public MainViewModel(DrawTerrainWorkflow workflow)
    {
        _workflow = workflow;
    }

    [RelayCommand]
    private void BrowseMap()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Warcraft III Maps (*.w3x;*.w3m)|*.w3x;*.w3m|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            MapFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseBmp()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bitmap Images (*.bmp)|*.bmp|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            BmpFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseCliffBmp()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bitmap Images (*.bmp)|*.bmp|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            CliffBmpFilePath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseWaterPng()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PNG Images (*.png)|*.png|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() == true)
        {
            WaterPngFilePath = dialog.FileName;
        }
    }

    private bool CanDrawTerrain() =>
        !IsBusy && !string.IsNullOrWhiteSpace(MapFilePath) && !string.IsNullOrWhiteSpace(BmpFilePath);

    [RelayCommand(CanExecute = nameof(CanDrawTerrain))]
    private async Task DrawTerrainAsync()
    {
        IsBusy = true;
        LogEntries.Clear();

        var progress = new Progress<string>(message => LogEntries.Add(message));

        try
        {
            string? cliffBmpPath = string.IsNullOrWhiteSpace(CliffBmpFilePath) ? null : CliffBmpFilePath;
            string? waterPngPath = string.IsNullOrWhiteSpace(WaterPngFilePath) ? null : WaterPngFilePath;
            await _workflow.RunAsync(MapFilePath, BmpFilePath, cliffBmpPath, waterPngPath, progress);
        }
        catch (Exception ex)
        {
            LogEntries.Add($"ERROR: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnBmpFilePathChanged(string value)
    {
        _ = RefreshBmpPreviewAsync();
    }

    partial void OnCliffBmpFilePathChanged(string value)
    {
        _ = RefreshCliffPreviewAsync();
    }

    partial void OnWaterPngFilePathChanged(string value)
    {
        _ = RefreshWaterPreviewAsync();
    }

    partial void OnZoomPercentChanged(double value)
    {
        RecomputeLayout();
    }

    // Loads the chosen BMP, caching it so zoom changes afterward only need to recompute layout,
    // not reload the file.
    // Public (rather than only fired as a side effect of the property setter) so it can be awaited
    // directly in tests instead of racing the fire-and-forget call above.
    public Task RefreshBmpPreviewAsync()
    {
        string path = BmpFilePath;
        int token = ++previewRequestToken;

        if (string.IsNullOrWhiteSpace(path))
        {
            loadedBitmap = null;
            ClearPreview();
            return Task.CompletedTask;
        }

        if (!File.Exists(path))
        {
            loadedBitmap = null;
            ClearPreview();
            PreviewError = $"ERROR: BMP file not found: {path}";
            return Task.CompletedTask;
        }

        try
        {
            BitmapImage bitmap = LoadFrozenBitmap(path);

            if (token != previewRequestToken)
            {
                return Task.CompletedTask; // superseded by a newer BMP selection
            }

            loadedBitmap = bitmap;
            BmpPreviewSource = bitmap;
            PreviewError = string.Empty;
            RecomputeLayout();
        }
        catch (Exception ex)
        {
            if (token == previewRequestToken)
            {
                loadedBitmap = null;
                ClearPreview();
                PreviewError = $"ERROR: {ex.Message}";
            }
        }

        return Task.CompletedTask;
    }

    // Loads the chosen cliff BMP for the overlay preview only — it plays no role in layout
    // math (the overlay is stretched to the already-computed PreviewWidth/Height), so no
    // RecomputeLayout call is needed here.
    public Task RefreshCliffPreviewAsync()
    {
        string path = CliffBmpFilePath;
        int token = ++cliffPreviewRequestToken;

        if (string.IsNullOrWhiteSpace(path))
        {
            loadedCliffBitmap = null;
            ClearCliffPreview();
            return Task.CompletedTask;
        }

        if (!File.Exists(path))
        {
            loadedCliffBitmap = null;
            ClearCliffPreview();
            CliffPreviewError = $"ERROR: Cliff BMP file not found: {path}";
            return Task.CompletedTask;
        }

        try
        {
            BitmapImage bitmap = LoadFrozenBitmap(path);

            if (token != cliffPreviewRequestToken)
            {
                return Task.CompletedTask; // superseded by a newer cliff BMP selection
            }

            loadedCliffBitmap = bitmap;
            CliffBmpPreviewSource = bitmap;
            CliffPreviewError = string.Empty;
        }
        catch (Exception ex)
        {
            if (token == cliffPreviewRequestToken)
            {
                loadedCliffBitmap = null;
                ClearCliffPreview();
                CliffPreviewError = $"ERROR: {ex.Message}";
            }
        }

        return Task.CompletedTask;
    }

    // Loads the chosen water PNG for the overlay preview only — same shape as
    // RefreshCliffPreviewAsync, no RecomputeLayout involvement.
    public Task RefreshWaterPreviewAsync()
    {
        string path = WaterPngFilePath;
        int token = ++waterPreviewRequestToken;

        if (string.IsNullOrWhiteSpace(path))
        {
            loadedWaterBitmap = null;
            ClearWaterPreview();
            return Task.CompletedTask;
        }

        if (!File.Exists(path))
        {
            loadedWaterBitmap = null;
            ClearWaterPreview();
            WaterPreviewError = $"ERROR: Water PNG file not found: {path}";
            return Task.CompletedTask;
        }

        try
        {
            BitmapImage bitmap = LoadFrozenBitmap(path);

            if (token != waterPreviewRequestToken)
            {
                return Task.CompletedTask; // superseded by a newer water PNG selection
            }

            loadedWaterBitmap = bitmap;
            WaterBmpPreviewSource = bitmap;
            WaterPreviewError = string.Empty;
        }
        catch (Exception ex)
        {
            if (token == waterPreviewRequestToken)
            {
                loadedWaterBitmap = null;
                ClearWaterPreview();
                WaterPreviewError = $"ERROR: {ex.Message}";
            }
        }

        return Task.CompletedTask;
    }

    // Recomputes every size-dependent preview value (display dimensions, border rectangle,
    // crosshair position) from the cached bitmap plus the current zoom level. Called after a
    // new BMP loads and again whenever the user changes zoom, without re-decoding the image.
    private void RecomputeLayout()
    {
        if (loadedBitmap is null)
        {
            return;
        }

        double nativeWidth = loadedBitmap.PixelWidth;
        double nativeHeight = loadedBitmap.PixelHeight;

        double clampedZoomPercent = Math.Clamp(ZoomPercent, MinZoomPercent, MaxZoomPercent);
        double baselineScale = Math.Min(1.0, MaxPreviewDimension / Math.Max(nativeWidth, nativeHeight));
        double totalScale = baselineScale * (clampedZoomPercent / 100.0);

        PreviewWidth = nativeWidth * totalScale;
        PreviewHeight = nativeHeight * totalScale;

        double left = PlayableAreaLeftInset;
        double top = PlayableAreaTopInset;
        double right = Math.Max(left, nativeWidth - PlayableAreaRightInset);
        double bottom = Math.Max(top, nativeHeight - PlayableAreaBottomInset);

        BorderLeft = left * totalScale;
        BorderTop = top * totalScale;
        BorderWidth = (right - left) * totalScale;
        BorderHeight = (bottom - top) * totalScale;

        CrosshairX = (left + right) / 2.0 * totalScale;
        CrosshairY = (top + bottom) / 2.0 * totalScale;
    }

    // Decodes fully into memory (CacheOption.OnLoad) so the file handle can be released
    // immediately, and freezes the result so it's safe to hand to data-binding.
    private static BitmapImage LoadFrozenBitmap(string path)
    {
        using FileStream stream = File.OpenRead(path);

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ClearPreview()
    {
        BmpPreviewSource = null;
        PreviewWidth = 0;
        PreviewHeight = 0;
        BorderLeft = 0;
        BorderTop = 0;
        BorderWidth = 0;
        BorderHeight = 0;
        CrosshairX = 0;
        CrosshairY = 0;
        PreviewError = string.Empty;
    }

    private void ClearCliffPreview()
    {
        CliffBmpPreviewSource = null;
        CliffPreviewError = string.Empty;
    }

    private void ClearWaterPreview()
    {
        WaterBmpPreviewSource = null;
        WaterPreviewError = string.Empty;
    }
}
