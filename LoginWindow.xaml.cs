using MahApps.Metro.Controls;
using StorageClient.Redesign.ViewModels;

namespace StorageClient.Redesign;

public partial class LoginWindow : MetroWindow
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
