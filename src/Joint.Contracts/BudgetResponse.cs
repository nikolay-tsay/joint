namespace Joint.Contracts;

/// <summary>
/// How the household's everyday spending this calendar month stands against its budget.
/// The budget does not cover tracked subscriptions and regular payments; those are
/// reported alongside. Amounts are positive numbers in one currency; transfers between
/// the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the household's transactions are in; null when there are none.</param>
/// <param name="Limit">What the household allows itself for everyday spending per month; null when no budget is set.</param>
/// <param name="Spent">Everyday spending so far this month: everything except subscriptions and regular payments.</param>
/// <param name="FixedPaid">What went to subscriptions and regular payments this month, outside the budget.</param>
/// <param name="FixedExpected">Subscriptions and regular payments paid last month but not yet this month.</param>
/// <param name="Projected">Where everyday spending is heading by month end, at the pace so far.</param>
/// <param name="LastMonthSpent">Last month's everyday spending, for a sense of what is usual.</param>
/// <param name="Month">The first day of the month all this is about.</param>
/// <param name="Categories">
/// The categories given part of the budget, by name; empty without any. What the budget leaves
/// to the rest is <paramref name="Limit"/> less their limits, against <paramref name="Spent"/> less their spending.
/// </param>
public record BudgetResponse(
    string? Currency,
    decimal? Limit,
    decimal Spent,
    decimal FixedPaid,
    decimal FixedExpected,
    decimal Projected,
    decimal LastMonthSpent,
    DateOnly Month,
    List<CategoryBudgetResponse>? Categories = null);
