using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wartergen.App.Services;
using Wartergen.Mpq;

namespace Wartergen.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DrawTerrainWorkflow _workflow;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string mapFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private string bmpFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DrawTerrainCommand))]
    private bool isBusy;

    public ObservableCollection<string> LogEntries { get; } = new();

    public MainViewModel() : this(new DrawTerrainWorkflow(MpqEditorService.CreateDefault()))
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
}
