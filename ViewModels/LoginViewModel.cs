using StorageClient.Redesign.Services;
using System.Net.Http;
using System.Windows.Input;

namespace StorageClient.Redesign.ViewModels;

public sealed class LoginViewModel : ViewModelBase
{
    private readonly IAuthenticationService _authenticationService;
    private readonly IAppSettings _settings;
    private readonly IThemeService _themeService;
    private readonly AsyncRelayCommand _signInCommand;
    private readonly AsyncRelayCommand _signInAsDifferentUserCommand;
    private readonly RelayCommand _saveServerAndRestartCommand;

    private string _serverAddress;
    private string _savedServerAddress;
    private string _errorMessage = string.Empty;
    private bool _isBusy;
    private bool _isSettingsOpen;
    private bool? _dialogResult;

    public LoginViewModel(
        IAuthenticationService authenticationService,
        IAppSettings settings,
        IThemeService themeService)
    {
        _authenticationService = authenticationService;
        _settings = settings;
        _themeService = themeService;

        _savedServerAddress = settings.BaseAddress;
        _serverAddress = _savedServerAddress;

        _signInCommand = new AsyncRelayCommand(() => SignInAsync(false), () => !IsBusy);
        _signInAsDifferentUserCommand = new AsyncRelayCommand(() => SignInAsync(true), () => !IsBusy);
        _saveServerAndRestartCommand = new RelayCommand(SaveServerAndRestart, CanSaveServer);

        OpenSettingsCommand = new RelayCommand(() => IsSettingsOpen = true, () => !IsBusy);
        CloseSettingsCommand = new RelayCommand(() => IsSettingsOpen = false);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
    }

    public ICommand SignInCommand => _signInCommand;
    public ICommand SignInAsDifferentUserCommand => _signInAsDifferentUserCommand;
    public ICommand SaveServerAndRestartCommand => _saveServerAndRestartCommand;
    public ICommand OpenSettingsCommand { get; }
    public ICommand CloseSettingsCommand { get; }
    public ICommand ToggleThemeCommand { get; }

    public string ServerAddress
    {
        get => _serverAddress;
        set
        {
            if (!Set(ref _serverAddress, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsServerAddressModified));
            OnPropertyChanged(nameof(IsServerAddressValid));
            OnPropertyChanged(nameof(ServerAddressValidationMessage));
            _saveServerAndRestartCommand.RaiseCanExecuteChanged();
        }
    }

    public string ActiveServerAddress => _savedServerAddress.TrimEnd('/');

    public bool IsServerAddressModified =>
        !string.Equals(
            NormalizeAddress(ServerAddress),
            NormalizeAddress(_savedServerAddress),
            StringComparison.OrdinalIgnoreCase);

    public bool IsServerAddressValid => TryValidateServerAddress(ServerAddress, out _);

    public string ServerAddressValidationMessage
    {
        get
        {
            TryValidateServerAddress(ServerAddress, out string message);
            return message;
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value))
            {
                return;
            }

            OnPropertyChanged(nameof(IsNotBusy));
            _signInCommand.RaiseCanExecuteChanged();
            _signInAsDifferentUserCommand.RaiseCanExecuteChanged();
            _saveServerAndRestartCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsNotBusy => !IsBusy;

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => Set(ref _isSettingsOpen, value);
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (Set(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool IsDarkTheme => _themeService.IsDarkTheme;

    public bool? DialogResult
    {
        get => _dialogResult;
        private set => Set(ref _dialogResult, value);
    }

    public bool RestartRequested { get; private set; }

    private async Task SignInAsync(bool forceAccountSelection)
    {
        try
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            await _authenticationService.SignInAsync(forceAccountSelection);
            DialogResult = true;
        }
        catch (HttpRequestException exception)
        {
            ErrorMessage = ApiErrorFormatter.Format(exception);
        }
        catch (Exception exception)
        {
            ErrorMessage = string.IsNullOrWhiteSpace(exception.Message)
                ? "Не удалось выполнить вход. Повторите попытку."
                : exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanSaveServer() =>
        !IsBusy && IsServerAddressModified && IsServerAddressValid;

    private void SaveServerAndRestart()
    {
        try
        {
            _settings.SaveBaseAddress(ServerAddress);
            _savedServerAddress = NormalizeAddress(ServerAddress);
            OnPropertyChanged(nameof(ActiveServerAddress));
            OnPropertyChanged(nameof(IsServerAddressModified));

            IsSettingsOpen = false;
            RestartRequested = true;
            DialogResult = false;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Не удалось сохранить адрес API: {exception.Message}";
        }
    }

    private void ToggleTheme()
    {
        _themeService.ToggleTheme();
        OnPropertyChanged(nameof(IsDarkTheme));
    }

    private static bool TryValidateServerAddress(string value, out string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            message = "Укажите адрес API.";
            return false;
        }

        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            message = "Нужен полный адрес, начинающийся с https:// или http://.";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private static string NormalizeAddress(string value)
    {
        string normalized = value.Trim();
        return normalized.EndsWith('/') ? normalized : normalized + "/";
    }
}
