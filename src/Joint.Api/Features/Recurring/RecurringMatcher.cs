using Joint.Api.Domain;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// Finds the tracked payment a transaction counts towards. When several match texts are
/// contained in one transaction, the longest wins, being the most specific.
/// </summary>
public class RecurringMatcher(IEnumerable<RecurringPayment> payments)
{
    private readonly List<RecurringPayment> _payments = payments
        .OrderByDescending(p => p.MatchKey.Length)
        .ThenBy(p => p.MatchKey, StringComparer.Ordinal)
        .ToList();

    public RecurringPayment? Find(string? counterparty, string? description) =>
        _payments.FirstOrDefault(p => PaymentKey.Matches(p.MatchKey, counterparty, description));

    /// <summary>
    /// Whether a payment was marked as money moved to one of the household's own accounts.
    /// Insights, Budget and Categories all drop these through this one test, so they agree
    /// on what spending is. Only money going out: whatever arrives under the same name is
    /// still income, at the user's request.
    /// </summary>
    public bool IsOwnTransfer(decimal amount, string? counterparty, string? description) =>
        amount < 0 && Find(counterparty, description)?.Kind == RecurringKind.Transfer;
}
