using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Wartergen.App.Models;
using Wartergen.App.Services;
using Wartergen.Mpq;

namespace Wartergen.App.ViewModels;

public partial class TerrainDataViewModel : ObservableObject
{
    private readonly TerrainJsonLoader loader;
    private JsonDocument? currentDocument;

    [ObservableProperty]
    private string mapFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ObservableCollection<JsonTreeNode> RootNodes { get; } = new();
    public ObservableCollection<JsonTreeNode> MeaningfulNodes { get; } = new();

    public TerrainDataViewModel() : this(new TerrainJsonLoader(MpqEditorService.CreateDefault()))
    {
    }

    public TerrainDataViewModel(TerrainJsonLoader loader)
    {
        this.loader = loader;
    }

    private bool CanBrowse() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseAsync()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Warcraft III Maps (*.w3x;*.w3m)|*.w3x;*.w3m|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        RootNodes.Clear();
        MeaningfulNodes.Clear();
        currentDocument?.Dispose();
        currentDocument = null;

        try
        {
            currentDocument = await loader.LoadAsync(dialog.FileName);
            RootNodes.Add(JsonTreeBuilder.CreateNode("$", currentDocument.RootElement));
            MeaningfulNodes.Add(MeaningfulDataTreeBuilder.CreateNode("$", currentDocument.RootElement));
            MapFilePath = dialog.FileName;
        }
        catch (Exception ex)
        {
            StatusMessage = $"ERROR: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
