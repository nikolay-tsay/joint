using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Joint.Mobile.Config;
using Joint.Mobile.Features.Lock;

namespace Joint.Mobile.Features.Auth;

/// <summary>
/// Owns the signed-in session: Google sign-in through Supabase, keeping the session
/// in the device's secure storage, and renewing the access token before it expires.
/// </summary>
public class AuthService(SupabaseAuthClient supabase, AppLock appLock)
{
    private const string StorageKey = "auth_session";
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private AuthSession? _session;

    /// <summary>Loads a saved session on app start. False means the user must sign in.</summary>
    public async Task<bool> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        var saved = await SecureStorage.Default.GetAsync(StorageKey);
        if (saved is null)
        {
            return false;
        }

        try
        {
            _session = JsonSerializer.Deserialize<AuthSession>(saved);
        }
        catch (JsonException)
        {
            _session = null;
        }

        try
        {
            return await GetAccessTokenAsync(cancellationToken) is not null;
        }
        catch (HttpRequestException)
        {
            // Offline: keep the saved session and let later calls retry the refresh.
            return _session is not null;
        }
    }

    /// <summary>Opens the browser for Google sign-in and stores the resulting session.</summary>
    /// <exception cref="TaskCanceledException">The user closed the browser.</exception>
    public async Task SignInWithGoogleAsync(CancellationToken cancellationToken = default)
    {
        // PKCE: the verifier stays on the device, so a stolen redirect code is useless.
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        var result = await WebAuthenticator.Default.AuthenticateAsync(
            supabase.BuildGoogleSignInUrl(challenge),
            new Uri(AppConfig.AuthCallbackUrl));

        if (!result.Properties.TryGetValue("code", out var code) || string.IsNullOrEmpty(code))
        {
            result.Properties.TryGetValue("error_description", out var error);
            throw new InvalidOperationException(error ?? "Sign-in did not return a code.");
        }

        await SaveAsync(await supabase.ExchangeCodeAsync(code, verifier, cancellationToken));
    }

    /// <summary>A valid access token, renewed if needed; null when the user is signed out.</summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_session is null)
            {
                return null;
            }

            if (_session.ExpiresWithin(RefreshMargin))
            {
                try
                {
                    await SaveAsync(await supabase.RefreshAsync(_session.RefreshToken, cancellationToken));
                }
                catch (HttpRequestException exception) when (exception.StatusCode is not null)
                {
                    // Supabase rejected the refresh token: the session is over.
                    Clear();
                    return null;
                }
            }

            return _session.AccessToken;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        var session = _session;
        Clear();
        if (session is null)
        {
            return;
        }

        try
        {
            await supabase.SignOutAsync(session.AccessToken, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Offline: the local session is gone, which is what matters on this device.
        }
    }

    private async Task SaveAsync(AuthSession session)
    {
        _session = session;
        await SecureStorage.Default.SetAsync(StorageKey, JsonSerializer.Serialize(session));
    }

    private void Clear()
    {
        _session = null;
        SecureStorage.Default.Remove(StorageKey);

        // The lock belongs to the session: the next person to sign in starts without one.
        appLock.Reset();
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
