namespace Joint.Mobile.Features.Auth;

/// <summary>A Supabase session: a short-lived access token and the token that renews it.</summary>
public record AuthSession(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    public bool ExpiresWithin(TimeSpan window) => ExpiresAt - DateTimeOffset.UtcNow <= window;
}
