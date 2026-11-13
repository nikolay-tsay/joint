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

    /// <summary>Sets whether an account counts as everyday, savings or investment.</summary>
    /// <returns>False when the account is not in the caller's household.</returns>
    public async Task<bool> SetAccountKindAsync(Guid accountId, string kind, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Patch, $"api/accounts/{accountId}", new UpdateAccountRequest(kind),
            cancellationToken, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode;
    }

    /// <summary>The household's provider connections and their sync state.</summary>
    public async Task<List<ConnectionResponse>> GetConnectionsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/connections", null, cancellationToken);
        return await ReadAsync<List<ConnectionResponse>>(response, cancellationToken);
    }

    /// <summary>The household's transactions, newest first; pass an account to narrow them.</summary>
    public async Task<List<TransactionResponse>> GetTransactionsAsync(
        Guid? accountId, string? kind, int skip, int limit, CancellationToken cancellationToken = default)
    {
        var path = $"api/transactions?skip={skip}&limit={limit}";
        if (accountId is { } id)
        {
            path += $"&accountId={id}";
        }

        if (!string.IsNullOrEmpty(kind))
        {
            path += $"&kind={kind}";
        }

        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        return await ReadAsync<List<TransactionResponse>>(response, cancellationToken);
    }

    /// <summary>The household's monthly budget and how this month stands against it.</summary>
    public async Task<BudgetResponse> GetBudgetAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/budget", null, cancellationToken);
        return await ReadAsync<BudgetResponse>(response, cancellationToken);
    }

    /// <summary>Sets the household's monthly budget; null removes it.</summary>
    /// <returns>False when the amount was not accepted.</returns>
    public async Task<bool> SetBudgetAsync(decimal? limit, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Put, "api/budget", new SetBudgetRequest(limit), cancellationToken, HttpStatusCode.BadRequest);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// One month's spending by category and the category names on offer. Null asks for
    /// the current month.
    /// </summary>
    public async Task<CategoriesResponse> GetCategoriesAsync(DateOnly? month, CancellationToken cancellationToken = default)
    {
        var path = month is { } first ? $"api/categories?month={first:yyyy-MM}" : "api/categories";
        using var response = await SendAsync(HttpMethod.Get, path, null, cancellationToken);
        return await ReadAsync<CategoriesResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Gives a payment's payee a category: every payment containing
    /// <paramref name="matchText"/> then has it. A new name makes a new category.
    /// </summary>
    /// <returns>False when the text or the name does not fit, or the payment is no longer there.</returns>
    public async Task<bool> AssignCategoryAsync(
        Guid transactionId, string category, string? matchText, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/categories", new AssignCategoryRequest(transactionId, category, matchText),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Takes the category off a payee.</summary>
    public async Task RemoveCategoryRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete, $"api/categories/{ruleId}", null, cancellationToken, HttpStatusCode.NotFound);
    }

    /// <summary>The household's saving goals, each with how much is saved.</summary>
    public async Task<List<GoalResponse>> GetGoalsAsync(CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, "api/goals", null, cancellationToken);
        return await ReadAsync<List<GoalResponse>>(response, cancellationToken);
    }

    /// <summary>Adds a saving goal, or changes the one with the given id.</summary>
    /// <returns>False when it was not accepted, or the goal is no longer there.</returns>
    public async Task<bool> SaveGoalAsync(Guid? goalId, SaveGoalRequest goal, CancellationToken cancellationToken = default)
    {
        using var response = goalId is { } id
            ? await SendAsync(
                HttpMethod.Put, $"api/goals/{id}", goal, cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.NotFound)
            : await SendAsync(HttpMethod.Post, "api/goals", goal, cancellationToken, HttpStatusCode.BadRequest);
        return response.IsSuccessStatusCode;
    }

    public async Task DeleteGoalAsync(Guid goalId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete, $"api/goals/{goalId}", null, cancellationToken, HttpStatusCode.NotFound);
    }

    /// <summary>Income, spending and who spent, for the charts on the Insights tab.</summary>
    public async Task<InsightsResponse> GetInsightsAsync(int months, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/insights?months={months}", null, cancellationToken);
        return await ReadAsync<InsightsResponse>(response, cancellationToken);
    }

    /// <summary>The household's subscriptions and regular payments, with what they cost per month.</summary>
    public async Task<RecurringOverviewResponse> GetRecurringAsync(int months, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(HttpMethod.Get, $"api/recurring?months={months}", null, cancellationToken);
        return await ReadAsync<RecurringOverviewResponse>(response, cancellationToken);
    }

    /// <summary>
    /// Tracks a payment as a subscription or a regular payment: every payment containing
    /// <paramref name="matchText"/> is then counted with it.
    /// </summary>
    /// <returns>False when the text does not fit the payment, or the payment cannot be tracked.</returns>
    public async Task<bool> MarkRecurringAsync(
        Guid transactionId, string kind, string? matchText, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/recurring", new MarkRecurringRequest(transactionId, kind, matchText),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode;
    }

    /// <summary>Stops tracking a subscription or regular payment.</summary>
    public async Task RemoveRecurringAsync(Guid recurringId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete, $"api/recurring/{recurringId}", null, cancellationToken, HttpStatusCode.NotFound);
    }

    /// <summary>Takes a payment out of the budget's figures, or puts it back.</summary>
    /// <returns>False when the transaction is not a payment or is no longer there.</returns>
    public async Task<bool> SetBudgetExclusionAsync(Guid transactionId, bool excluded, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Patch, $"api/transactions/{transactionId}", new UpdateTransactionRequest(excluded),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode;
    }

    /// <summary>
    /// Asks the backend to sync a connection now. Debug builds only: the backend does not
    /// offer this in production.
    /// </summary>
    public async Task SyncConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, $"api/connections/{connectionId}/sync", null, cancellationToken, HttpStatusCode.NotFound);
    }

    /// <summary>Removes one of the caller's connections with its accounts and transactions.</summary>
    public async Task DeleteConnectionAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Delete, $"api/connections/{connectionId}", null, cancellationToken, HttpStatusCode.NotFound);
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

    /// <returns>Null on success, otherwise a message to show the user.</returns>
    public async Task<string?> ConnectTrading212Async(
        string apiKey, string apiSecret, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/connections/trading212", new ConnectTrading212Request(apiKey, apiSecret),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest =>
                "Trading 212 rejected this key. Check the key and secret, and that the key has the "
                + "Account data and Portfolio permissions.",
            HttpStatusCode.BadGateway => "Trading 212 could not be reached. Try again shortly.",
            _ => null,
        };
    }

    /// <returns>Null on success, otherwise a message to show the user.</returns>
    public async Task<string?> ConnectFioAsync(string token, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Post, "api/connections/fio", new ConnectFioRequest(token),
            cancellationToken, HttpStatusCode.BadRequest, HttpStatusCode.BadGateway);
        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest => "Fio rejected this token. Check that it was copied whole and has not expired.",
            HttpStatusCode.BadGateway => "Fio could not be reached. Try again in a minute.",
            _ => null,
        };
    }

    /// <summary>What an investment account currently holds, largest position first.</summary>
    public async Task<List<HoldingResponse>> GetHoldingsAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            HttpMethod.Get, $"api/accounts/{accountId}/holdings", null, cancellationToken, HttpStatusCode.NotFound);
        return response.IsSuccessStatusCode
            ? await ReadAsync<List<HoldingResponse>>(response, cancellationToken)
            : [];
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
