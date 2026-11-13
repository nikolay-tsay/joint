using Joint.Api.Features.Insights;
using Joint.Api.Features.Recurring;
using Joint.Contracts;

namespace Joint.Api.Features.Budget;

/// <summary>Works out where the current month stands and where it is heading.</summary>
public static class BudgetCalculator
{
    /// <param name="flows">The household's transactions from the start of last month.</param>
    /// <param name="recurring">The outgoing ones among them, each with the tracked payment it counts towards.</param>
    public static BudgetResponse Build(
        decimal? limit, IReadOnlyList<FlowRow> flows, IReadOnlyList<RecurringRow> recurring, DateOnly today)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        // Same rules as the Insights charts, so the two screens never disagree on "spent".
        var insights = InsightsCalculator.Build(flows, today, months: 2);
        var spent = insights.Months[1].Spending;

        var tracked = recurring.Where(r => r.Key is not null && r.Amount < 0 && r.Currency == insights.Currency).ToList();
        var paidThisMonth = PaidPerPayment(tracked, thisMonth);
        var paidLastMonth = PaidPerPayment(tracked, lastMonth);

        // Never more than was spent: a tracked payment can also have been dropped as a transfer.
        var fixedPaid = Math.Min(paidThisMonth.Values.Sum(), spent);

        // What came last month and has not come yet is assumed to still be on its way.
        var fixedExpected = paidLastMonth
            .Where(p => !paidThisMonth.ContainsKey(p.Key))
            .Sum(p => p.Value);

        // Fixed costs land on their own dates, often early, so only the rest is stretched
        // over the month. Stretching rent paid on the 1st would predict thirty rents.
        var daysInMonth = DateTime.DaysInMonth(today.Year, today.Month);
        var everydayPace = (spent - fixedPaid) / today.Day * daysInMonth;

        return new BudgetResponse(
            insights.Currency,
            limit,
            spent,
            fixedPaid,
            fixedExpected,
            Projected: decimal.Round(fixedPaid + fixedExpected + everydayPace, 2),
            LastMonthSpent: insights.Months[0].Spending,
            thisMonth);
    }

    private static Dictionary<string, decimal> PaidPerPayment(List<RecurringRow> tracked, DateOnly month) =>
        tracked
            .Where(r => r.BookedOn.Year == month.Year && r.BookedOn.Month == month.Month)
            .GroupBy(r => r.Key!)
            .ToDictionary(g => g.Key, g => g.Sum(r => -r.Amount));
}
