namespace Joint.Api.Features.Recurring;

/// <summary>One transaction, reduced to what the recurring overview needs.</summary>
/// <param name="Key">Its <see cref="PaymentKey"/>, or null when it names no payee.</param>
public record RecurringRow(DateOnly BookedOn, decimal Amount, string Currency, string? Key);
