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

    private readonly DrawTerrainWorkflow _workflow;
    private readonly PlayableAreaTemplateProvider _templateProvider;
    private int previewRequestToken;
    private BitmapImage? loadedBitmap;
    private PlayableAreaTemplate? loadedTemplate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string mapFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string bmpFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private bool isBusy;

    [ObservableProperty]
    private ImageSource? bmpPreviewSource;

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
    private string previewError = string.Empty;

    public ObservableCollection<string> LogEntries { get; } = new();

    public MainViewModel()
        : this(new DrawTerrainWorkflow(MpqEditorService.CreateDefault()), PlayableAreaTemplateProvider.CreateDefault())
    {
    }

    public MainViewModel(DrawTerrainWorkflow workflow)
        : this(workflow, PlayableAreaTemplateProvider.CreateDefault())
    {
    }

    public MainViewModel(DrawTerrainWorkflow workflow, PlayableAreaTemplateProvider templateProvider)
    {
        _workflow = workflow;
        _templateProvider = templateProvider;
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
            await _workflow.RunAsync(MapFilePath, BmpFilePath, progress);
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

    partial void OnZoomPercentChanged(double value)
    {
        RecomputeLayout();
    }

    // Loads the chosen BMP and the reference playable-area template (derived once from
    // samples/BoundariesTest.w3x, cached thereafter), caching both so zoom changes afterward
    // only need to recompute layout, not reload anything.
    // Public (rather than only fired as a side effect of the property setter) so it can be awaited
    // directly in tests instead of racing the fire-and-forget call above.
    public async Task RefreshBmpPreviewAsync()
    {
        string path = BmpFilePath;
        int token = ++previewRequestToken;

        if (string.IsNullOrWhiteSpace(path))
        {
            loadedBitmap = null;
            loadedTemplate = null;
            ClearPreview();
            return;
        }

        if (!File.Exists(path))
        {
            loadedBitmap = null;
            loadedTemplate = null;
            ClearPreview();
            PreviewError = $"ERROR: BMP file not found: {path}";
            return;
        }

        try
        {
            BitmapImage bitmap = LoadFrozenBitmap(path);
            PlayableAreaTemplate template = await _templateProvider.GetAsync();

            if (token != previewRequestToken)
            {
                return; // superseded by a newer BMP selection
            }

            loadedBitmap = bitmap;
            loadedTemplate = template;
            BmpPreviewSource = bitmap;
            PreviewError = string.Empty;
            RecomputeLayout();
        }
        catch (Exception ex)
        {
            if (token == previewRequestToken)
            {
                loadedBitmap = null;
                loadedTemplate = null;
                ClearPreview();
                PreviewError = $"ERROR: {ex.Message}";
            }
        }
    }

    // Recomputes every size-dependent preview value (display dimensions, border rectangle,
    // crosshair position) from the cached bitmap/template plus the current zoom level. Called
    // after a new BMP loads and again whenever the user changes zoom, without re-decoding the
    // image or re-fetching the template.
    private void RecomputeLayout()
    {
        if (loadedBitmap is null || loadedTemplate is null)
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

        PlayableAreaTemplate template = loadedTemplate;
        double left = template.LeftInset;
        double top = template.TopInset;
        double right = Math.Max(left, nativeWidth - template.RightInset);
        double bottom = Math.Max(top, nativeHeight - template.BottomInset);

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
}
