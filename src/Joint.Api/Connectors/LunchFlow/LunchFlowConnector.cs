using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Joint.Api.Domain;

namespace Joint.Api.Connectors.LunchFlow;

/// <summary>
/// Lunch Flow's Personal API. The user connects their banks in Lunch Flow and gives us the
/// API key of an "API destination"; one key can expose accounts from several banks.
/// Lunch Flow refreshes its data about once a day, so frequent polling gains nothing.
/// </summary>
public class LunchFlowConnector(HttpClient http) : IProviderConnector
{
    public const string BaseUrl = "https://lunchflow.app/api/v1/";

    public ProviderKind Provider => ProviderKind.LunchFlow;

    public async Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken)
    {
        try
        {
            await GetAsync<AccountsResponse>("accounts", credential, cancellationToken);
            return true;
        }
        catch (ProviderAuthenticationException)
        {
            return false;
        }
    }

    public async Task<ProviderSnapshot> FetchAsync(
        string credential, DateOnly? since, CancellationToken cancellationToken)
    {
        var listed = await GetAsync<AccountsResponse>("accounts", credential, cancellationToken);
        var accounts = new List<ProviderAccount>();

        foreach (var account in listed.Accounts ?? [])
        {
            if (!string.Equals(account.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var balance = await GetAsync<BalanceResponse>(
                $"accounts/{account.Id}/balance", credential, cancellationToken);
            var from = since is { } date ? $"?from={date:yyyy-MM-dd}" : "";
            var transactions = await GetAsync<TransactionsResponse>(
                $"accounts/{account.Id}/transactions{from}", credential, cancellationToken);

            var currency = balance.Balance?.Currency ?? account.Currency ?? "";
            accounts.Add(new ProviderAccount(
                ExternalId: account.Id.ToString(),
                Name: account.Name ?? account.InstitutionName ?? "Account",
                Institution: account.InstitutionName,
                Kind: AccountKind.Current,
                Currency: currency,
                Balance: balance.Balance?.Amount ?? 0,
                Transactions: (transactions.Transactions ?? [])
                    // Pending items can change or vanish before they are booked.
                    .Where(t => !t.IsPending && !string.IsNullOrEmpty(t.Id))
                    .Select(t => new ProviderTransaction(
                        t.Id!,
                        t.Date,
                        t.Amount,
                        string.IsNullOrEmpty(t.Currency) ? currency : t.Currency,
                        NullIfBlank(t.Merchant),
                        NullIfBlank(t.Description)))
                    .ToList(),
                Holdings: []));
        }

        return new ProviderSnapshot(accounts);
    }

    private async Task<T> GetAsync<T>(string path, string apiKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("x-api-key", apiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ProviderAuthenticationException("Lunch Flow rejected the API key.");
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
            ?? throw new HttpRequestException($"Lunch Flow returned an empty response for {path}.");
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record AccountsResponse(List<AccountDto>? Accounts);

    private sealed record AccountDto(
        long Id,
        string? Name,
        [property: JsonPropertyName("institution_name")] string? InstitutionName,
        string? Currency,
        string? Status);

    private sealed record BalanceResponse(BalanceDto? Balance);

    private sealed record BalanceDto(decimal Amount, string? Currency);

    private sealed record TransactionsResponse(List<TransactionDto>? Transactions);

    private sealed record TransactionDto(
        string? Id,
        decimal Amount,
        string? Currency,
        DateOnly Date,
        string? Merchant,
        string? Description,
        bool IsPending);
}
