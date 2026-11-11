using Microsoft.Extensions.DependencyInjection;
using StorageClient.Redesign.Services;
using StorageClient.Redesign.ViewModels;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;

namespace StorageClient.Redesign;

public partial class App : Application
{
    private ServiceProvider? _services;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ru-RU");
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage("ru-RU")));

        _services = ConfigureServices();
        _services.GetRequiredService<IThemeService>().ApplySavedTheme();

        ShowLogin();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        base.OnExit(e);
    }

    private void ShowLogin()
    {
        if (_services is null)
        {
            Shutdown();
            return;
        }

        LoginWindow loginWindow = _services.GetRequiredService<LoginWindow>();
        LoginViewModel loginViewModel = (LoginViewModel)loginWindow.DataContext;
        bool? result = loginWindow.ShowDialog();

        if (loginViewModel.RestartRequested)
        {
            RestartApplication();
            return;
        }

        if (result == true)
        {
            MainWindow mainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Closed += (_, _) => Shutdown();
            mainWindow.Show();
            return;
        }

        Shutdown();
    }

    private static ServiceProvider ConfigureServices()
    {
        ServiceCollection services = new();

        services.AddSingleton<IAppSettings, AppSettings>();
        services.AddSingleton<IAuthStateService, AuthStateService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAuthenticationService, OidcAuthenticationService>();
        services.AddSingleton<IContainerService, ContainerService>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<LoginWindow>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<MainWindow>();

        return services.BuildServiceProvider();
    }

    private static void RestartApplication()
    {
        string? executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            MessageBox.Show(
                "Адрес API сохранён. Перезапустите приложение вручную.",
                "Требуется перезапуск",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Current.Shutdown();
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = true
        });
        Current.Shutdown();
    }
}
