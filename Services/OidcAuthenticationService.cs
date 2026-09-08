using Duende.IdentityModel.OidcClient;
using StorageClient.Redesign.DTOs;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace StorageClient.Redesign.Services;

public interface IAuthenticationService
{
    Task SignInAsync(bool forceAccountSelection = false);
}

public sealed class OidcAuthenticationService : IAuthenticationService
{
    private readonly IAppSettings _settings;
    private readonly IAuthStateService _authState;

    public OidcAuthenticationService(IAppSettings settings, IAuthStateService authState)
    {
        _settings = settings;
        _authState = authState;
    }

    public async Task SignInAsync(bool forceAccountSelection = false)
    {
        OidcClient oidcClient = CreateOidcClient();

        LoginResult loginResult = forceAccountSelection
            ? await oidcClient.LoginAsync(new LoginRequest
            {
                FrontChannelExtraParameters =
                {
                    { "prompt", "login" }
                }
            })
            : await oidcClient.LoginAsync();

        if (loginResult.IsError || string.IsNullOrWhiteSpace(loginResult.AccessToken))
        {
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(loginResult.Error)
                    ? "Сервер авторизации не вернул токен доступа."
                    : loginResult.Error);
        }

        using HttpClient client = new()
        {
            BaseAddress = new Uri(_settings.BaseAddress, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", loginResult.AccessToken);

        using HttpResponseMessage response = await client.GetAsync("api/Auth/me");
        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                string.IsNullOrWhiteSpace(body)
                    ? $"API вернул ошибку {(int)response.StatusCode}."
                    : body,
                null,
                response.StatusCode);
        }

        CurrentUserDto currentUser =
            await response.Content.ReadFromJsonAsync<CurrentUserDto>()
            ?? throw new InvalidDataException("API вернул пустые данные пользователя.");

        _authState.SignIn(
            loginResult.AccessToken,
            loginResult.RefreshToken,
            loginResult.AccessTokenExpiration.UtcDateTime,
            currentUser.Username,
            currentUser.Permission,
            currentUser.UserId);
    }

    private OidcClient CreateOidcClient() =>
        new(new OidcClientOptions
        {
            Authority = _settings.OidcAuthority,
            ClientId = _settings.OidcClientId,
            RedirectUri = _settings.OidcRedirectUri,
            Scope = _settings.OidcScope,
            Browser = new SystemBrowser(_settings.OidcRedirectUri)
        });
}
