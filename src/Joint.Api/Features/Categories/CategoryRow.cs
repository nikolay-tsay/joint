namespace Joint.Api.Features.Categories;

/// <summary>One transaction, reduced to what the category breakdown needs.</summary>
/// <param name="MarkedAsTransfer">A payment the household marked as an own transfer; see <c>RecurringMatcher.IsOwnTransfer</c>.</param>
public record CategoryRow(
    Guid AccountId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    string? Counterparty,
    string? Description,
    bool MarkedAsTransfer = false);
