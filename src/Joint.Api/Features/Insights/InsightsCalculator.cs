using Joint.Contracts;

namespace Joint.Api.Features.Insights;

/// <summary>Turns a household's recent transactions into the series the Insights charts draw.</summary>
public static class InsightsCalculator
{
    /// <param name="months">How many calendar months to cover, the current one included.</param>
    public static InsightsResponse Build(IReadOnlyList<FlowRow> rows, DateOnly today, int months)
    {
        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var lastMonth = thisMonth.AddMonths(-1);

        // Amounts are never converted, so the charts follow the currency the household mostly uses.
        var currency = rows
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        var flows = InternalTransfers.Without(
            rows.Where(r => r.Currency == currency).ToList(), r => r.AccountId, r => r.BookedOn, r => r.Amount, r => r.MarkedAsTransfer);

        var monthly = Enumerable.Range(0, months)
            .Select(back => thisMonth.AddMonths(back - months + 1))
            .Select(month => new MonthFlowResponse(
                month,
                Income: flows.Where(r => IsIn(r, month) && r.Amount > 0).Sum(r => r.Amount),
                Spending: flows.Where(r => IsIn(r, month) && r.Amount < 0).Sum(r => -r.Amount)))
            .ToList();

        var members = flows
            .Where(r => IsIn(r, thisMonth) && r.Amount < 0)
            .GroupBy(r => r.OwnerId)
            .Select(g => new MemberSpendingResponse(g.First().OwnerName, g.First().OwnerAvatarUrl, g.Sum(r => -r.Amount)))
            .OrderByDescending(m => m.Spending)
            .ToList();

        return new InsightsResponse(
            currency,
            monthly,
            RunningSpending(flows, thisMonth, days: today.Day),
            RunningSpending(flows, lastMonth, days: DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month)),
            members);
    }

    /// <summary>Spending so far on each day of a month: entry 0 is the 1st.</summary>
    private static List<decimal> RunningSpending(List<FlowRow> flows, DateOnly month, int days)
    {
        var perDay = flows
            .Where(r => IsIn(r, month) && r.Amount < 0)
            .GroupBy(r => r.BookedOn.Day)
            .ToDictionary(g => g.Key, g => g.Sum(r => -r.Amount));

        var running = new List<decimal>(days);
        var total = 0m;
        for (var day = 1; day <= days; day++)
        {
            total += perDay.GetValueOrDefault(day);
            running.Add(total);
        }

        return running;
    }

    private static bool IsIn(FlowRow row, DateOnly month) =>
        row.BookedOn.Year == month.Year && row.BookedOn.Month == month.Month;
}
