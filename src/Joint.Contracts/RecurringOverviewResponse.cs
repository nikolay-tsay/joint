namespace Joint.Contracts;

/// <summary>Everything the household tracks as a subscription or a regular payment.</summary>
/// <param name="Currency">The currency the monthly totals are in; null when nothing tracked was paid.</param>
/// <param name="Payments">Grouped by kind, each biggest first. Own transfers are listed too but are in no total.</param>
/// <param name="Months">Oldest first, ending with the current month.</param>
public record RecurringOverviewResponse(
    string? Currency, List<RecurringPaymentResponse> Payments, List<RecurringMonthResponse> Months);
