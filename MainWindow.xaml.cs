using MahApps.Metro.Controls;
using StorageClient.Redesign.ViewModels;
using System.Windows;

namespace StorageClient.Redesign;

public partial class MainWindow : MetroWindow
{
    private bool _isLoaded;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isLoaded)
        {
            return;
        }

        _isLoaded = true;
        await ((MainViewModel)DataContext).LoadInitialAsync();
    }
}
