using Joint.Api.Domain;
using Joint.Api.Features.Recurring;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Finds the category rule a transaction falls under. When several match texts are
/// contained in one transaction, the longest wins, being the most specific.
/// </summary>
public class CategoryMatcher(IEnumerable<CategoryRule> rules)
{
    private readonly List<CategoryRule> _rules = rules
        .OrderByDescending(r => r.MatchKey.Length)
        .ThenBy(r => r.MatchKey, StringComparer.Ordinal)
        .ToList();

    public CategoryRule? Find(string? counterparty, string? description) =>
        _rules.FirstOrDefault(r => PaymentKey.Matches(r.MatchKey, counterparty, description));
}
