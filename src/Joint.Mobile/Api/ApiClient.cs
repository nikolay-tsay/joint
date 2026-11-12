using System.Net.Http.Headers;
using System.Net.Http.Json;
using Joint.Contracts;
using Joint.Mobile.Config;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Api;

/// <summary>Calls the Joint backend as the signed-in user.</summary>
public class ApiClient(AuthService auth)
{
    private readonly HttpClient _http = new() { BaseAddress = new Uri(AppConfig.ApiBaseUrl) };

    /// <summary>The signed-in user and their household. Also registers a first-time user.</summary>
    public Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default) =>
        GetAsync<MeResponse>("api/me", cancellationToken);

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        var token = await auth.GetAccessTokenAsync(cancellationToken)
            ?? throw new SignedOutException();

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            throw new SignedOutException();
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException($"The server returned an empty response for {path}.");
    }
}
