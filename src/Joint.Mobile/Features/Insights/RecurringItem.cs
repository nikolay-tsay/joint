using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Insights;

/// <summary>One tracked subscription or regular payment, formatted for display.</summary>
/// <param name="Detail">When it was last paid.</param>
/// <param name="Amount">What was last paid; empty when nothing was paid in the past year.</param>
public record RecurringItem(Guid Id, string Name, string Detail, string Amount)
{
    public static RecurringItem From(RecurringPaymentResponse payment) =>
        new(
            payment.Id,
            payment.Name,
            payment.LastPaidOn is { } paidOn ? $"Last paid {paidOn:d MMMM}" : "Not paid in the past year",
            payment.LastAmount is { } amount ? Money.Format(amount, payment.Currency ?? "") : "");
}
