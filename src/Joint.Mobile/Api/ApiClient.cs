using System.Net;
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
    public async Task<MeResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/me", null, cancellationToken);
        return await ReadAsync<MeResponse>(response, cancellationToken);
    }

    /// <summary>Creates a code another person can use to join the caller's household.</summary>
    public async Task<CreateInvitationResponse> CreateInvitationAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/household/invitations", null, cancellationToken);
        return await ReadAsync<CreateInvitationResponse>(response, cancellationToken);
    }

    /// <returns>False when the code is invalid, used, expired or the caller's own.</returns>
    public async Task<bool> AcceptInvitationAsync(string code, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/household/invitations/accept", new AcceptInvitationRequest(code),
            cancellationToken, HttpStatusCode.NotFound, HttpStatusCode.Conflict);
        return response.IsSuccessStatusCode;
    }

    public async Task LeaveHouseholdAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Post, "api/household/leave", null, cancellationToken);
    }

    /// <summary>Every account in the household, with current balances.</summary>
    public async Task<List<AccountResponse>> GetAccountsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/accounts", null, cancellationToken);
        return await ReadAsync<List<AccountResponse>>(response, cancellationToken);
    }

    /// <summary>The household's provider connections and their sync state.</summary>
    public async Task<List<ConnectionResponse>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/connections", null, cancellationToken);
        return await ReadAsync<List<ConnectionResponse>>(response, cancellationToken);
    }

    /// <returns>Null on success, otherwise a message to show the user.</returns>
    public async Task<string?> ConnectLunchFlowAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/connections/lunch-flow", new ConnectLunchFlowRequest(apiKey),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Lunch Flow rejected this API key.",
            HttpStatusCode.BadGateway => "Lunch Flow could not be reached. Try again shortly.",
            _ => null,
        };
    }

    /// <param name="expected">Non-success statuses the caller handles itself; any other failure throws.</param>
    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method, string path, object? body, CancellationToken cancellationToken,
        params HttpStatusCode[] expected)
    {
        var token = await auth.GetAccessTokenAsync(cancellationToken)
            ?? throw new SignedOutException();

        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        var response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            throw new SignedOutException();
        }

        if (!response.IsSuccessStatusCode && !expected.Contains(response.StatusCode))
        {
            using (response)
            {
                response.EnsureSuccessStatusCode();
            }
        }

        return response;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new InvalidOperationException("The server returned an empty response.");
}
