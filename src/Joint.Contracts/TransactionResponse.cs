namespace Joint.Contracts;

/// <summary>A booked transaction. Negative amounts are money leaving the account.</summary>
/// <param name="OwnerDisplayName">The household member whose account this is, so who made it.</param>
/// <param name="OwnerAvatarUrl">That member's profile picture, if they have one.</param>
/// <param name="RecurringId">The tracked payment this one counts towards, if any.</param>
/// <param name="RecurringKind">Subscription or Bill when it counts towards one.</param>
public record TransactionResponse(
    Guid Id,
    Guid AccountId,
    string AccountName,
    string OwnerDisplayName,
    string? OwnerAvatarUrl,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description,
    Guid? RecurringId = null,
    string? RecurringKind = null);
