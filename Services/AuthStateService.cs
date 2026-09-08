namespace StorageClient.Redesign.Services;

public interface IAuthStateService
{
    bool IsAuthenticated { get; }
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTime? AccessTokenExpiresAt { get; }
    string? Username { get; }
    string Permission { get; }
    int? UserId { get; }

    void SignIn(
        string accessToken,
        string? refreshToken,
        DateTime? accessTokenExpiresAt,
        string? username,
        string? permission,
        int userId);
    void SignOut();
}

public sealed class AuthStateService : IAuthStateService
{
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(AccessToken);
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTime? AccessTokenExpiresAt { get; private set; }
    public string? Username { get; private set; }
    public string Permission { get; private set; } = "Guest";
    public int? UserId { get; private set; }

    public void SignIn(
        string accessToken,
        string? refreshToken,
        DateTime? accessTokenExpiresAt,
        string? username,
        string? permission,
        int userId)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        AccessTokenExpiresAt = accessTokenExpiresAt;
        Username = username;
        Permission = string.IsNullOrWhiteSpace(permission) ? "Guest" : permission;
        UserId = userId;
    }

    public void SignOut()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAt = null;
        Username = null;
        Permission = "Guest";
        UserId = null;
    }
}
