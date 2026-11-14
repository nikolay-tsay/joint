namespace Joint.Api.Connectors;

/// <summary>
/// A transaction as a provider reports it, booked unless it is in <see cref="ProviderAccount.Pending"/>.
/// Negative amounts are money leaving the account.
/// </summary>
public record ProviderTransaction(
    string ExternalId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description);
