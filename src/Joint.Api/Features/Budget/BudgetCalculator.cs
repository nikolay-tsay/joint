using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;

namespace Joint.Api.Features.Budget;

/// <summary>
/// Works out where the current month stands and where it is heading. The budget is for
/// everyday spending only: what goes to tracked subscriptions and regular payments is a
/// given, reported next to it and never counted against it.
/// </summary>
public static class BudgetCalculator
{
    /// <param name="flows">The household's transactions from the start of last month.</param>
    /// <param name="recurring">The outgoing ones among them, each with the tracked payment it counts towards.</param>
    public static BudgetResponse Build(
        decimal? limit, IReadOnlyList<FlowRow> flows, IReadOnlyList<RecurringRow> recurring, DateOnly today)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        // Same rules as the Insights charts, so the two screens never disagree on what was spent.
        var insights = InsightsCalculator.Build(flows, today, months: 2);
        var spentInTotal = insights.Months[1].Spending;
        var spentLastMonth = insights.Months[0].Spending;

        var tracked = recurring.Where(r => r.Key is not null && r.Amount < 0 && r.Currency == insights.Currency).ToList();
        var paidThisMonth = PaidPerPayment(tracked, thisMonth);
        var paidLastMonth = PaidPerPayment(tracked, lastMonth);

        // Never more than was spent: a tracked payment can also have been dropped as a transfer.
        var fixedPaid = Math.Min(paidThisMonth.Values.Sum(), spentInTotal);
        var fixedLastMonth = Math.Min(paidLastMonth.Values.Sum(), spentLastMonth);

        // What came last month and has not come yet is assumed to still be on its way.
        var fixedExpected = paidLastMonth
            .Where(p => !paidThisMonth.ContainsKey(p.Key))
            .Sum(p => p.Value);

        var everyday = spentInTotal - fixedPaid;
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);

        return new BudgetResponse(
            insights.Currency,
            limit,
            Spent: everyday,
            fixedPaid,
            fixedExpected,
            Projected: decimal.Round(everyday / today.Day * daysInMonth, 2),
            LastMonthSpent: spentLastMonth - fixedLastMonth,
            thisMonth);
    }

    private static Dictionary<string, decimal> PaidPerPayment(List<RecurringRow> tracked, DateOnly month) =>
        tracked
            .Where(r => r.BookedOn.Year == month.Year && r.BookedOn.Month == month.Month)
            .GroupBy(r => r.Key!)
            .ToDictionary(g => g.Key, g => g.Sum(r => -r.Amount));
}
