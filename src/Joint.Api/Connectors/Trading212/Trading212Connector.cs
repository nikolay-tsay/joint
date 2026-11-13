using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Joint.Api.Domain;

namespace Joint.Api.Connectors.Trading212;

/// <summary>
/// Trading 212's public API (v0, beta) for an Invest or Stocks ISA account. Reads the
/// account's total value and its open positions; it never places orders. The user creates
/// an API key with the "Account data" and "Portfolio" permissions and gives us key and secret.
/// </summary>
public class Trading212Connector(HttpClient http) : IProviderConnector
{
    public const string BaseUrl = "https://live.trading212.com/api/v0/";

    /// <summary>How long to wait at most for a rate limit to clear before giving up.</summary>
    private static readonly TimeSpan MaxRateLimitWait = TimeSpan.FromSeconds(10);

    public ProviderKind Provider => ProviderKind.Trading212;

    /// <summary>Waits out a rate limit. Replaceable so tests don't sleep.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    /// <summary>Packs the two halves of a Trading 212 credential into the one string that gets stored.</summary>
    public static string PackCredential(string apiKey, string apiSecret) => $"{apiKey.Trim()}\n{apiSecret.Trim()}";

    public async Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken)
    {
        try
        {
            // Both calls, so a key missing either permission is caught now, not at the first sync.
            await GetAsync<AccountSummary>("equity/account/summary", credential, cancellationToken);
            await GetAsync<List<Position>>("equity/positions", credential, cancellationToken);
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
        var summary = await GetAsync<AccountSummary>("equity/account/summary", credential, cancellationToken);
        var positions = await GetAsync<List<Position>>("equity/positions", credential, cancellationToken);
        var currency = summary.Currency ?? "";

        var holdings = positions
            .Where(p => !string.IsNullOrEmpty(p.Instrument?.Ticker))
            .Select(p => new ProviderHolding(
                Ticker: p.Instrument!.Ticker!,
                Name: p.Instrument.Name ?? p.Instrument.Ticker!,
                Quantity: p.Quantity,
                AveragePrice: p.AveragePricePaid,
                CurrentPrice: p.CurrentPrice,
                Currency: p.Instrument.Currency ?? currency,
                Value: p.WalletImpact?.CurrentValue ?? 0,
                UnrealizedProfitLoss: p.WalletImpact?.UnrealizedProfitLoss ?? 0))
            .ToList();

        // One Trading 212 key covers exactly one account. Trades and dividends are not
        // imported as transactions; the app shows this account through its holdings.
        return new ProviderSnapshot([
            new ProviderAccount(
                ExternalId: summary.Id.ToString(),
                Name: "Trading 212",
                Institution: "Trading 212",
                Kind: AccountKind.Investment,
                Currency: currency,
                Balance: summary.TotalValue,
                Transactions: [],
                Holdings: holdings),
        ]);
    }

    private async Task<T> GetAsync<T>(string path, string credential, CancellationToken cancellationToken)
    {
        // Limits are per account and tight (the summary allows one call per five seconds),
        // so a call made right after another one is expected to be told to wait.
        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", ToBasicToken(credential));

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new ProviderAuthenticationException(
                    "Trading 212 rejected the API key, or it lacks the Account data or Portfolio permission.");
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests && attempt < 2)
            {
                await Delay(RateLimitWait(response), cancellationToken);
                continue;
            }

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<T>(cancellationToken)
                ?? throw new HttpRequestException($"Trading 212 returned an empty response for {path}.");
        }
    }

    /// <summary>Until the moment the response says the limit resets, within reason.</summary>
    private static TimeSpan RateLimitWait(HttpResponseMessage response)
    {
        var wait = TimeSpan.FromSeconds(5);
        if (response.Headers.TryGetValues("x-ratelimit-reset", out var values)
            && long.TryParse(values.FirstOrDefault(), out var resetAt))
        {
            wait = DateTimeOffset.FromUnixTimeSeconds(resetAt) - DateTimeOffset.UtcNow + TimeSpan.FromMilliseconds(500);
        }

        return wait < TimeSpan.FromSeconds(1) ? TimeSpan.FromSeconds(1)
            : wait > MaxRateLimitWait ? MaxRateLimitWait
            : wait;
    }

    private static string ToBasicToken(string credential)
    {
        var parts = credential.Split('\n', 2);
        if (parts.Length != 2)
        {
            throw new ProviderAuthenticationException("The saved Trading 212 credential is incomplete.");
        }

        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{parts[0]}:{parts[1]}"));
    }

    private sealed record AccountSummary(long Id, string? Currency, decimal TotalValue);

    private sealed record Position(
        decimal Quantity,
        decimal AveragePricePaid,
        decimal CurrentPrice,
        Instrument? Instrument,
        WalletImpact? WalletImpact);

    private sealed record Instrument(string? Ticker, string? Name, string? Currency);

    private sealed record WalletImpact(decimal CurrentValue, decimal UnrealizedProfitLoss);
}
