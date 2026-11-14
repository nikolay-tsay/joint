using Joint.Contracts;

namespace Joint.Api.Features.Insights;

/// <summary>Turns a household's recent transactions into the series the Insights charts draw.</summary>
public static class InsightsCalculator
{
    /// <param name="rows">The household's transactions from the start of the oldest period on.</param>
    /// <param name="months">How many periods to cover, the chosen one included.</param>
    /// <param name="period">The period reported on; the calendar month of <paramref name="today"/> when null.</param>
    public static InsightsResponse Build(IReadOnlyList<FlowRow> rows, DateOnly today, int months, DatePeriod? period = null)
    {
        var current = period ?? DatePeriod.MonthOf(today);
        var previous = current.Shift(-1);

        // Amounts are never converted, so the charts follow the currency the household mostly uses.
        var currency = rows
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        var flows = InternalTransfers.Without(
            rows.Where(r => r.Currency == currency).ToList(), r => r.AccountId, r => r.BookedOn, r => r.Amount, r => r.MarkedAsTransfer);

        var periods = Enumerable.Range(0, months)
            .Select(back => current.Shift(back - months + 1))
            .Select(p => new MonthFlowResponse(
                p.From,
                Income: flows.Where(r => p.Contains(r.BookedOn) && r.Amount > 0).Sum(r => r.Amount),
                Spending: flows.Where(r => p.Contains(r.BookedOn) && r.Amount < 0).Sum(r => -r.Amount),
                p.To))
            .ToList();

        var members = flows
            .Where(r => current.Contains(r.BookedOn) && r.Amount < 0)
            .GroupBy(r => r.OwnerId)
            .Select(g => new MemberSpendingResponse(g.First().OwnerName, g.First().OwnerAvatarUrl, g.Sum(r => -r.Amount)))
            .OrderByDescending(m => m.Spending)
            .ToList();

        // The chosen period runs only up to today; a past one is shown in full.
        var daysSoFar = Math.Clamp(today.DayNumber - current.From.DayNumber + 1, 0, current.Days);
        return new InsightsResponse(
            currency,
            periods,
            RunningSpending(flows, current, daysSoFar),
            RunningSpending(flows, previous, previous.Days),
            members,
            current.From,
            current.To);
    }

    /// <summary>Spending so far on each day of a period: entry 0 is its first day.</summary>
    private static List<decimal> RunningSpending(List<FlowRow> flows, DatePeriod period, int days)
    {
        var perDay = flows
            .Where(r => period.Contains(r.BookedOn) && r.Amount < 0)
            .GroupBy(r => r.BookedOn.DayNumber - period.From.DayNumber)
            .ToDictionary(g => g.Key, g => g.Sum(r => -r.Amount));

        var running = new List<decimal>(days);
        var total = 0m;
        for (var day = 0; day < days; day++)
        {
            total += perDay.GetValueOrDefault(day);
            running.Add(total);
        }

        return running;
    }
}
