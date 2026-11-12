namespace Joint.Contracts;

/// <summary>A booked transaction. Negative amounts are money leaving the account.</summary>
public record TransactionResponse(
    Guid Id,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description);
