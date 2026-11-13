namespace Joint.Contracts;

/// <summary>What subscriptions and regular payments cost in one calendar month, as positive numbers.</summary>
/// <param name="Month">The first day of the month.</param>
public record RecurringMonthResponse(DateOnly Month, decimal Subscriptions, decimal Bills);
