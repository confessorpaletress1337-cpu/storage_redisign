using ControlzEx.Theming;
using MaterialDesignThemes.Wpf;
using System.Windows;

namespace StorageClient.Redesign.Services;

public interface IThemeService
{
    AppTheme CurrentTheme { get; }
    bool IsDarkTheme { get; }

    void ApplySavedTheme();
    void ToggleTheme();
}

public sealed class ThemeService : IThemeService
{
    private readonly IAppSettings _settings;

    public ThemeService(IAppSettings settings)
    {
        _settings = settings;
        CurrentTheme = settings.Theme;
    }

    public AppTheme CurrentTheme { get; private set; }
    public bool IsDarkTheme => CurrentTheme == AppTheme.Dark;

    public void ApplySavedTheme() => ApplyTheme(_settings.Theme, savePreference: false);

    public void ToggleTheme() =>
        ApplyTheme(IsDarkTheme ? AppTheme.Light : AppTheme.Dark, savePreference: true);

    private void ApplyTheme(AppTheme theme, bool savePreference)
    {
        if (Application.Current is null)
        {
            return;
        }

        bool isDark = theme == AppTheme.Dark;
        var dictionaries = Application.Current.Resources.MergedDictionaries;
        ResourceDictionary? currentColors = dictionaries.FirstOrDefault(dictionary =>
            dictionary.Source?.OriginalString.EndsWith(
                "/Themes/Colors.Light.xaml",
                StringComparison.OrdinalIgnoreCase) == true
            || dictionary.Source?.OriginalString.EndsWith(
                "/Themes/Colors.Dark.xaml",
                StringComparison.OrdinalIgnoreCase) == true);

        ResourceDictionary replacement = new()
        {
            Source = new Uri(
                isDark ? "/Themes/Colors.Dark.xaml" : "/Themes/Colors.Light.xaml",
                UriKind.Relative)
        };

        if (currentColors is null)
        {
            dictionaries.Add(replacement);
        }
        else
        {
            dictionaries[dictionaries.IndexOf(currentColors)] = replacement;
        }

        PaletteHelper paletteHelper = new();
        var materialTheme = paletteHelper.GetTheme();
        materialTheme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
        paletteHelper.SetTheme(materialTheme);

        ThemeManager.Current.ChangeTheme(
            Application.Current,
            isDark ? ThemeManager.BaseColorDark : ThemeManager.BaseColorLight,
            "Teal");

        CurrentTheme = theme;
        if (savePreference)
        {
            _settings.SaveTheme(theme);
        }
    }
}
