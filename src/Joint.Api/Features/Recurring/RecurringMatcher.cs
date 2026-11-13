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
    /// Whether a transaction, in either direction, was marked as money moved between the
    /// household's own accounts. Insights, Budget and Categories all drop these through
    /// this one test, so they agree on what spending and income are.
    /// </summary>
    public bool IsOwnTransfer(string? counterparty, string? description) =>
        Find(counterparty, description)?.Kind == RecurringKind.Transfer;
}
