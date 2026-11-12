using Joint.Api.Domain;

namespace Joint.Api.Connectors;

public record ProviderAccount(
    string ExternalId,
    string Name,
    string? Institution,
    AccountKind Kind,
    string Currency,
    decimal Balance,
    IReadOnlyList<ProviderTransaction> Transactions,
    IReadOnlyList<ProviderHolding> Holdings);
