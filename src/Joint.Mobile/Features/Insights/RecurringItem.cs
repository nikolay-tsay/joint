using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Insights;

/// <summary>One tracked subscription or regular payment, formatted for display.</summary>
/// <param name="Detail">When it was last paid, and the text its payments are recognized by.</param>
/// <param name="Amount">What was last paid; empty when nothing was paid in the past year.</param>
public record RecurringItem(Guid Id, string Name, string Detail, string Amount)
{
    public static RecurringItem From(RecurringPaymentResponse payment) =>
        new(
            payment.Id,
            payment.Name,
            (payment.LastPaidOn is { } paidOn
                ? $"{(payment.Kind == RecurringKinds.Transfer ? "Last sent" : "Last paid")} {paidOn:d MMMM}"
                : payment.Kind == RecurringKinds.Transfer ? "Nothing sent in the past year" : "Not paid in the past year")
            + (payment.MatchText.Length > 0 ? $" · matches “{payment.MatchText}”" : ""),
            payment.LastAmount is { } amount ? Money.Format(amount, payment.Currency ?? "") : "");
}
