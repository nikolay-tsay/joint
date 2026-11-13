namespace Joint.Api.Features.Recurring;

/// <summary>One transaction, reduced to what the recurring overview needs.</summary>
/// <param name="Key">The match text of the tracked payment it counts towards, or null.</param>
public record RecurringRow(DateOnly BookedOn, decimal Amount, string Currency, string? Key);
