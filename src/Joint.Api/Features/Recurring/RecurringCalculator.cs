using Joint.Api.Domain;
using Joint.Contracts;

namespace Joint.Api.Features.Recurring;

/// <summary>Matches a household's transactions to its marked payees and totals them by month.</summary>
public static class RecurringCalculator
{
    /// <param name="rows">Transactions of the period looked back over, which may be longer than the chart.</param>
    /// <param name="months">How many calendar months the chart covers, the current one included.</param>
    public static RecurringOverviewResponse Build(
        IReadOnlyList<RecurringPayment> payments, IReadOnlyList<RecurringRow> rows, DateOnly today, int months)
    {
        // Two members may have marked the same payee; it is still one payment.
        var byKey = payments
            .GroupBy(p => p.MatchKey)
            .ToDictionary(g => g.Key, g => g.First());

        // Only money going out counts: a refund from a payee is not a payment to it.
        var paid = rows
            .Where(r => r.Amount < 0 && r.Key is not null && byKey.ContainsKey(r.Key))
            .Select(r => (Row: r, Payment: byKey[r.Key!]))
            .ToList();

        // Amounts are never converted, so the chart follows the currency most payments are in.
        var currency = paid
            .GroupBy(p => p.Row.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        var listed = byKey.Values
            .Select(payment =>
            {
                var last = paid
                    .Where(p => p.Payment == payment)
                    .OrderByDescending(p => p.Row.BookedOn)
                    .Select(p => p.Row)
                    .FirstOrDefault();
                return new RecurringPaymentResponse(
                    payment.Id, payment.Kind.ToString(), payment.Name, -last?.Amount, last?.Currency, last?.BookedOn);
            })
            .OrderBy(p => p.Kind)
            .ThenByDescending(p => p.LastAmount ?? 0)
            .ThenBy(p => p.Name)
            .ToList();

        var thisMonth = new DateOnly(today.Year, today.Month, 1);
        var monthly = Enumerable.Range(0, months)
            .Select(back => thisMonth.AddMonths(back - months + 1))
            .Select(month => new RecurringMonthResponse(
                month,
                Subscriptions: Total(month, RecurringKind.Subscription),
                Bills: Total(month, RecurringKind.Bill)))
            .ToList();

        return new RecurringOverviewResponse(currency, listed, monthly);

        decimal Total(DateOnly month, RecurringKind kind) =>
            paid.Where(p => p.Payment.Kind == kind
                    && p.Row.Currency == currency
                    && p.Row.BookedOn.Year == month.Year
                    && p.Row.BookedOn.Month == month.Month)
                .Sum(p => -p.Row.Amount);
    }
}
