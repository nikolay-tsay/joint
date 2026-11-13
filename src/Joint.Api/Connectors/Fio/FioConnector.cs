using System.Globalization;
using System.Net;
using System.Text.Json;
using Joint.Api.Domain;

namespace Joint.Api.Connectors.Fio;

/// <summary>
/// Fio banka's token API. The user creates a read-only token for one account in internet
/// banking (Settings, then API) and gives it to us; one token is one account. A single
/// request returns the balance and the movements of a period.
/// </summary>
public class FioConnector(HttpClient http) : IProviderConnector
{
    public const string BaseUrl = "https://fioapi.fio.cz/v1/rest/";

    /// <summary>
    /// Fio serves anything older than 90 days only for ten minutes after the user unlocks
    /// the token in internet banking (HTTP 422 otherwise), so we never ask for more.
    /// </summary>
    private const int DaysOfHistoryAvailable = 89;

    /// <summary>A token may be used once every 30 seconds; a second longer for safety.</summary>
    private static readonly TimeSpan RateLimitWait = TimeSpan.FromSeconds(31);

    public ProviderKind Provider => ProviderKind.Fio;

    /// <summary>Waits out the rate limit. Replaceable so tests don't sleep.</summary>
    public Func<TimeSpan, CancellationToken, Task> Delay { get; init; } = Task.Delay;

    public async Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken)
    {
        try
        {
            // The smallest request there is: today only.
            var today = Today();
            using var statement = await GetStatementAsync(credential, today, today, cancellationToken);
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
        var today = Today();
        var earliest = today.AddDays(-DaysOfHistoryAvailable);
        var from = since is { } date && date > earliest ? date : earliest;

        using var statement = await GetStatementAsync(credential, from, today, cancellationToken);
        if (!statement.RootElement.TryGetProperty("accountStatement", out var root)
            || !root.TryGetProperty("info", out var info))
        {
            throw new HttpRequestException("Fio returned a statement without account details.");
        }

        var accountNumber = $"{Text(info, "accountId")}/{Text(info, "bankId")}";
        var currency = Text(info, "currency") ?? "";
        var balance = info.TryGetProperty("closingBalance", out var closing) && closing.ValueKind == JsonValueKind.Number
            ? closing.GetDecimal()
            : 0;

        var transactions = new List<ProviderTransaction>();
        if (root.TryGetProperty("transactionList", out var list)
            && list.ValueKind == JsonValueKind.Object
            && list.TryGetProperty("transaction", out var rows)
            && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (ToTransaction(row, currency) is { } transaction)
                {
                    transactions.Add(transaction);
                }
            }
        }

        return new ProviderSnapshot(
        [
            new ProviderAccount(
                ExternalId: Text(info, "iban") ?? accountNumber,
                Name: accountNumber,
                Institution: "Fio banka",
                Kind: AccountKind.Current,
                Currency: currency,
                Balance: balance,
                Transactions: transactions,
                Holdings: []),
        ]);
    }

    /// <summary>
    /// A movement is a set of numbered columns: 22 id, 0 date, 1 amount, 14 currency,
    /// 10 counterparty name, 2 and 3 counterparty account and bank, 16 message for the
    /// recipient, 25 comment, 7 user identification (the merchant on card payments), 8 type.
    /// </summary>
    private static ProviderTransaction? ToTransaction(JsonElement row, string accountCurrency)
    {
        var id = Column(row, 22);
        var bookedOn = ToDate(row, 0);
        if (id is null || bookedOn is null
            || !row.TryGetProperty("column1", out var amountColumn)
            || amountColumn.ValueKind != JsonValueKind.Object
            || !amountColumn.TryGetProperty("value", out var amount)
            || amount.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        var counterpartyAccount = Column(row, 2) is { } number
            ? Column(row, 3) is { } bank ? $"{number}/{bank}" : number
            : null;

        return new ProviderTransaction(
            id,
            bookedOn.Value,
            amount.GetDecimal(),
            Column(row, 14) ?? accountCurrency,
            Column(row, 10) ?? counterpartyAccount,
            Column(row, 16) ?? Column(row, 25) ?? Column(row, 7) ?? Column(row, 8));
    }

    private async Task<JsonDocument> GetStatementAsync(
        string token, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var path = $"periods/{Uri.EscapeDataString(token)}/{from:yyyy-MM-dd}/{to:yyyy-MM-dd}/transactions.json";

        // The first sync runs seconds after the connect-time check, so being told to wait is expected.
        for (var attempt = 0; ; attempt++)
        {
            using var response = await http.GetAsync(path, cancellationToken);

            // Fio answers 500, not 401, for a token that does not exist or has expired.
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                throw new ProviderAuthenticationException("Fio does not recognize the token, or it has expired.");
            }

            if (response.StatusCode == HttpStatusCode.Conflict && attempt < 2)
            {
                await Delay(RateLimitWait, cancellationToken);
                continue;
            }

            if (!response.IsSuccessStatusCode)
            {
                // Not EnsureSuccessStatusCode: nothing derived from the request may carry the token.
                throw new HttpRequestException($"Fio answered {(int)response.StatusCode}.");
            }

            await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                return await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
            }
            catch (JsonException)
            {
                throw new HttpRequestException("Fio returned a response that is not JSON.");
            }
        }
    }

    private static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow);

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) ? AsText(value) : null;

    /// <summary>The value of a numbered column, or null when it is absent or blank.</summary>
    private static string? Column(JsonElement row, int id) =>
        row.TryGetProperty($"column{id}", out var column)
        && column.ValueKind == JsonValueKind.Object
        && column.TryGetProperty("value", out var value)
            ? AsText(value)
            : null;

    private static string? AsText(JsonElement value)
    {
        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };

        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    /// <summary>
    /// Dates come as "2026-11-10+0100" in practice, while the specification's examples show
    /// milliseconds since 1970 for midnight in Prague. Both are read.
    /// </summary>
    private static DateOnly? ToDate(JsonElement row, int id)
    {
        if (!row.TryGetProperty($"column{id}", out var column)
            || column.ValueKind != JsonValueKind.Object
            || !column.TryGetProperty("value", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var milliseconds))
        {
            // Midnight in Prague is late evening UTC the day before; half a day on lands on the right date.
            return DateOnly.FromDateTime(
                DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).AddHours(12).UtcDateTime);
        }

        return value.ValueKind == JsonValueKind.String
            && value.GetString() is { Length: >= 10 } text
            && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                ? date
                : null;
    }
}
