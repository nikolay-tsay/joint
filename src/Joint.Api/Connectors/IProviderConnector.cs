using Joint.Api.Domain;

namespace Joint.Api.Connectors;

/// <summary>
/// Reads accounts, transactions and holdings from one provider (a bank or a broker)
/// and returns them in the app's normalized shape. Connectors are read-only.
/// </summary>
public interface IProviderConnector
{
    ProviderKind Provider { get; }

    /// <param name="credential">The decrypted provider credential.</param>
    /// <param name="since">Fetch transactions booked on or after this date.</param>
    Task<ProviderSnapshot> FetchAsync(string credential, DateOnly since, CancellationToken cancellationToken);
}

public record ProviderSnapshot(IReadOnlyList<ProviderAccount> Accounts);

public record ProviderAccount(
    string ExternalId,
    string Name,
    AccountKind Kind,
    string Currency,
    decimal Balance,
    IReadOnlyList<ProviderTransaction> Transactions,
    IReadOnlyList<ProviderHolding> Holdings);

public record ProviderTransaction(
    string ExternalId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description);

public record ProviderHolding(
    string Ticker,
    string Name,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string Currency);
