using Joint.Api.Domain;

namespace Joint.Api.Connectors;

/// <param name="Transactions">Booked transactions only.</param>
/// <param name="Pending">
/// Transactions the bank has not booked yet, for a provider that reports them; all of the
/// account's current ones, since they replace what was stored. Their ids are ignored.
/// </param>
public record ProviderAccount(
    string ExternalId,
    string Name,
    string? Institution,
    AccountKind Kind,
    string Currency,
    decimal Balance,
    IReadOnlyList<ProviderTransaction> Transactions,
    IReadOnlyList<ProviderHolding> Holdings,
    IReadOnlyList<ProviderTransaction>? Pending = null);
