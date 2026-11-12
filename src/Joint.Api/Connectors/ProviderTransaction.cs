namespace Joint.Api.Connectors;

/// <summary>A booked transaction. Negative amounts are money leaving the account.</summary>
public record ProviderTransaction(
    string ExternalId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description);
