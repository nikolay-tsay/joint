namespace Joint.Contracts;

/// <summary>A booked transaction. Negative amounts are money leaving the account.</summary>
/// <param name="OwnerDisplayName">The household member whose account this is, so who made it.</param>
/// <param name="OwnerAvatarUrl">That member's profile picture, if they have one.</param>
/// <param name="RecurringId">The tracked payment this one counts towards, if any.</param>
/// <param name="RecurringKind">Subscription, Bill or Transfer when it counts towards one.</param>
/// <param name="RecurringMatch">
/// For a payment: the match text of the tracked payment it counts towards, or the
/// suggested text for tracking it. Null when it cannot be tracked.
/// </param>
/// <param name="Category">The category a rule gives this payment, if any.</param>
/// <param name="CategoryRuleId">The rule that gives it, for removing the category.</param>
/// <param name="CategoryMatch">
/// For a payment: the match text of its category rule, or the suggested text for making
/// one. Null when it cannot be categorized.
/// </param>
/// <param name="IsInternalTransfer">
/// True when the app recognises this as money moved between the household's own accounts:
/// the same amount left one account and arrived in another within two days. It is then
/// counted as neither income nor spending.
/// </param>
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
    string? RecurringKind = null,
    string? RecurringMatch = null,
    string? Category = null,
    Guid? CategoryRuleId = null,
    string? CategoryMatch = null,
    bool IsInternalTransfer = false);
