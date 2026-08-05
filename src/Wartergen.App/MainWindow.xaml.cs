using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using Wartergen.App.Models;
using Wartergen.App.ViewModels;

namespace Wartergen.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        if (DataContext is MainViewModel viewModel)
        {
            viewModel.LogEntries.CollectionChanged += LogEntries_CollectionChanged;
        }
    }

    private void LogEntries_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (LogListBox.Items.Count > 0)
        {
            LogListBox.ScrollIntoView(LogListBox.Items[^1]);
        }
    }

    private void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is TreeViewItem { DataContext: JsonTreeNode node })
        {
            node.EnsureChildrenLoaded();
        }
    }
}
