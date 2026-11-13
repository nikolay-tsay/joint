namespace Joint.Api.Features.Insights;

/// <summary>One transaction, reduced to what the insights need.</summary>
/// <param name="MarkedAsTransfer">A payment the household marked as an own transfer; see <c>RecurringMatcher.IsOwnTransfer</c>.</param>
public record FlowRow(
    Guid AccountId,
    Guid OwnerId,
    string OwnerName,
    string? OwnerAvatarUrl,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    bool MarkedAsTransfer = false);
