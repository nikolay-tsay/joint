using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Joint.Mobile.Config;

namespace Joint.Mobile.Features.Auth;

/// <summary>The few Supabase Auth HTTP calls the app needs.</summary>
public class SupabaseAuthClient
{
    private readonly HttpClient _http;

    public SupabaseAuthClient()
    {
        _http = new HttpClient { BaseAddress = new Uri(AppConfig.SupabaseUrl.TrimEnd('/') + "/auth/v1/") };
        _http.DefaultRequestHeaders.Add("apikey", AppConfig.SupabasePublishableKey);
    }

    /// <summary>The page to open in the browser to sign in with Google.</summary>
    public Uri BuildGoogleSignInUrl(string codeChallenge) =>
        new(_http.BaseAddress!,
            "authorize?provider=google"
            + $"&redirect_to={Uri.EscapeDataString(AppConfig.AuthCallbackUrl)}"
            + $"&code_challenge={codeChallenge}&code_challenge_method=s256");

    /// <summary>Trades the code from the sign-in redirect for a session.</summary>
    public Task<AuthSession> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken) =>
        RequestSessionAsync("token?grant_type=pkce", new { auth_code = code, code_verifier = codeVerifier }, cancellationToken);

    public Task<AuthSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken) =>
        RequestSessionAsync("token?grant_type=refresh_token", new { refresh_token = refreshToken }, cancellationToken);

    public async Task SignOutAsync(string accessToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, cancellationToken);
    }

    private async Task<AuthSession> RequestSessionAsync(string path, object body, CancellationToken cancellationToken)
    {
        using var response = await _http.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<TokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("Supabase returned an empty session.");
        return new AuthSession(
            token.AccessToken,
            token.RefreshToken,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn));
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("refresh_token")] string RefreshToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
