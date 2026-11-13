namespace Joint.Contracts;

/// <summary>
/// How the household's spending this calendar month stands against its budget. Amounts are
/// positive numbers in one currency; transfers between the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the household's transactions are in; null when there are none.</param>
/// <param name="Limit">What the household allows itself per month; null when no budget is set.</param>
/// <param name="Spent">Everything spent so far this month.</param>
/// <param name="FixedPaid">The part of <paramref name="Spent"/> that went to subscriptions and regular payments.</param>
/// <param name="FixedExpected">Subscriptions and regular payments paid last month but not yet this month.</param>
/// <param name="Projected">Where the month is heading: fixed costs in full, everything else at the pace so far.</param>
/// <param name="LastMonthSpent">Last month's total, for a sense of what is usual.</param>
/// <param name="Month">The first day of the month all this is about.</param>
public record BudgetResponse(
    string? Currency,
    decimal? Limit,
    decimal Spent,
    decimal FixedPaid,
    decimal FixedExpected,
    decimal Projected,
    decimal LastMonthSpent,
    DateOnly Month);
