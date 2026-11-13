using Joint.Contracts;

namespace Joint.Api.Features.Insights;

/// <summary>Turns a household's recent transactions into the series the Insights charts draw.</summary>
public static class InsightsCalculator
{
    /// <summary>Two halves of a transfer may be booked this many days apart.</summary>
    private const int TransferWindowDays = 2;

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
        var flows = WithoutInternalTransfers(rows.Where(r => r.Currency == currency).ToList());

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

    /// <summary>
    /// Drops money moved between the household's own accounts, which is neither income nor
    /// spending: an amount leaving one account and the same amount arriving in another
    /// within a couple of days. Nothing in the data marks a transfer, so this is a guess
    /// that can also swallow a coincidence, like paying 500 and being paid 500 the same day.
    /// </summary>
    private static List<FlowRow> WithoutInternalTransfers(List<FlowRow> rows)
    {
        var transfers = new HashSet<FlowRow>(ReferenceEqualityComparer.Instance);
        foreach (var sameAmount in rows.GroupBy(r => Math.Abs(r.Amount)))
        {
            var arrivals = sameAmount.Where(r => r.Amount > 0).OrderBy(r => r.BookedOn).ToList();
            foreach (var departure in sameAmount.Where(r => r.Amount < 0).OrderBy(r => r.BookedOn))
            {
                var arrival = arrivals.FirstOrDefault(a =>
                    a.AccountId != departure.AccountId
                    && Math.Abs(a.BookedOn.DayNumber - departure.BookedOn.DayNumber) <= TransferWindowDays);
                if (arrival is not null)
                {
                    arrivals.Remove(arrival);
                    transfers.Add(arrival);
                    transfers.Add(departure);
                }
            }
        }

        return rows.Where(r => !transfers.Contains(r)).ToList();
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
