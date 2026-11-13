namespace Joint.Contracts;

/// <summary>A tracked subscription or regular payment with its latest payment.</summary>
/// <param name="Kind">Subscription or Bill.</param>
/// <param name="LastAmount">What was last paid, as a positive number; null when nothing was paid in the past year.</param>
public record RecurringPaymentResponse(
    Guid Id, string Kind, string Name, decimal? LastAmount, string? Currency, DateOnly? LastPaidOn);
