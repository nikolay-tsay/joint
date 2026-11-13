namespace Joint.Api.Features.Insights;

/// <summary>
/// Recognises money moved between the household's own accounts, which is neither income
/// nor spending: an amount leaving one account and the same amount arriving in another
/// within a couple of days. Nothing in the data marks a transfer, so this is a guess that
/// can also swallow a coincidence, like paying 500 and being paid 500 the same day.
/// </summary>
public static class InternalTransfers
{
    /// <summary>Two halves of a transfer may be booked this many days apart.</summary>
    public const int WindowDays = 2;

    /// <summary>Both halves of every recognised transfer.</summary>
    /// <param name="rows">Transactions in one currency; amounts are matched as they are.</param>
    public static HashSet<T> Find<T>(
        IReadOnlyList<T> rows, Func<T, Guid> account, Func<T, DateOnly> bookedOn, Func<T, decimal> amount)
        where T : class
    {
        var transfers = new HashSet<T>(ReferenceEqualityComparer.Instance);
        foreach (var sameAmount in rows.GroupBy(r => Math.Abs(amount(r))))
        {
            var arrivals = sameAmount.Where(r => amount(r) > 0).OrderBy(bookedOn).ToList();
            foreach (var departure in sameAmount.Where(r => amount(r) < 0).OrderBy(bookedOn))
            {
                var arrival = arrivals.FirstOrDefault(a =>
                    account(a) != account(departure)
                    && Math.Abs(bookedOn(a).DayNumber - bookedOn(departure).DayNumber) <= WindowDays);
                if (arrival is not null)
                {
                    arrivals.Remove(arrival);
                    transfers.Add(arrival);
                    transfers.Add(departure);
                }
            }
        }

        return transfers;
    }

    /// <summary>
    /// The rows that are income or spending: both halves of every recognised transfer are
    /// taken out, and so is every payment the household marked as a transfer by hand.
    /// Pairing comes first and sees the marked payments too. Were they removed beforehand,
    /// a marked payment whose arrival the app can also see would leave that arrival without
    /// a partner, and it would count as income.
    /// </summary>
    /// <param name="rows">Transactions in one currency; amounts are matched as they are.</param>
    /// <param name="marked">Whether a row is a payment marked as an own transfer.</param>
    public static List<T> Without<T>(
        IReadOnlyList<T> rows,
        Func<T, Guid> account,
        Func<T, DateOnly> bookedOn,
        Func<T, decimal> amount,
        Func<T, bool> marked)
        where T : class
    {
        var transfers = Find(rows, account, bookedOn, amount);
        return rows.Where(r => !transfers.Contains(r) && !marked(r)).ToList();
    }
}
