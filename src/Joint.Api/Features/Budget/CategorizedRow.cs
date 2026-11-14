namespace Joint.Api.Features.Budget;

/// <summary>
/// A transaction as the budget's category lines see it: its category, and whether it counts
/// towards a tracked subscription or regular payment (then it is not everyday spending).
/// </summary>
/// <param name="MarkedAsTransfer">A payment the household marked as an own transfer; dropped after pairing.</param>
public record CategorizedRow(
    Guid AccountId,
    DateOnly BookedOn,
    decimal Amount,
    string Currency,
    bool MarkedAsTransfer,
    bool Tracked,
    string? Category);
