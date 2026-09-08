using System.Configuration;
using System.Text.Json;

namespace StorageClient.Redesign.Services;

public enum AppTheme
{
    Light,
    Dark
}

public interface IAppSettings
{
    string BaseAddress { get; }
    string OidcAuthority { get; }
    string OidcClientId { get; }
    string OidcRedirectUri { get; }
    string OidcScope { get; }
    AppTheme Theme { get; }

    void SaveBaseAddress(string baseAddress);
    void SaveTheme(AppTheme theme);
}

public sealed class AppSettings : IAppSettings
{
    private readonly string _settingsPath;
    private UserPreferences _preferences;

    public AppSettings()
    {
        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StorageClient.Redesign");

        _settingsPath = Path.Combine(settingsDirectory, "settings.json");
        _preferences = LoadPreferences(_settingsPath);
    }

    public string BaseAddress =>
        NormalizeBaseAddress(_preferences.BaseAddress ?? GetRequiredDefault("BaseAddress"));

    public string OidcAuthority => GetRequiredDefault("Keycloak.Authority");
    public string OidcClientId => GetRequiredDefault("Keycloak.ClientId");
    public string OidcRedirectUri => GetRequiredDefault("Keycloak.RedirectUri");
    public string OidcScope => GetRequiredDefault("Keycloak.Scope");

    public AppTheme Theme =>
        Enum.TryParse(_preferences.Theme ?? ConfigurationManager.AppSettings["ThemeName"], true, out AppTheme theme)
            ? theme
            : AppTheme.Light;

    public void SaveBaseAddress(string baseAddress)
    {
        _preferences.BaseAddress = NormalizeBaseAddress(baseAddress);
        SavePreferences();
    }

    public void SaveTheme(AppTheme theme)
    {
        _preferences.Theme = theme.ToString();
        SavePreferences();
    }

    private string GetRequiredDefault(string key) =>
        ConfigurationManager.AppSettings[key]
        ?? throw new InvalidOperationException($"Не задан параметр конфигурации: {key}");

    private void SavePreferences()
    {
        string? directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = JsonSerializer.Serialize(_preferences, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        File.WriteAllText(_settingsPath, json);
    }

    private static UserPreferences LoadPreferences(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return new UserPreferences();
            }

            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UserPreferences>(json) ?? new UserPreferences();
        }
        catch
        {
            return new UserPreferences();
        }
    }

    private static string NormalizeBaseAddress(string value)
    {
        string normalized = value.Trim();
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }

    private sealed class UserPreferences
    {
        public string? BaseAddress { get; set; }
        public string? Theme { get; set; }
    }
}
