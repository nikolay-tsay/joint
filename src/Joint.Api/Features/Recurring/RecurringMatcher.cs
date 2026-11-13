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
}
