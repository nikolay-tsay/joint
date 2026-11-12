namespace Joint.Contracts;

/// <summary>A booked transaction. Negative amounts are money leaving the account.</summary>
/// <param name="OwnerDisplayName">The household member whose account this is, so who made it.</param>
public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string OwnerDisplayName,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description);
