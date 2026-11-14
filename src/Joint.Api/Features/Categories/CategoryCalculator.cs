using Joint.Api.Domain;
using Joint.Api.Features.Insights;
using Joint.Contracts;

namespace Joint.Api.Features.Categories;

/// <summary>Splits a month's spending by category.</summary>
public static class CategoryCalculator
{
    /// <param name="rows">
    /// The household's transactions of the month and a few days either side, so a transfer
    /// whose two halves straddle the month's edge is still recognised.
    /// </param>
    /// <param name="month">The first day of the month to report.</param>
    /// <param name="hidden">Starter categories the household deleted.</param>
    public static CategoriesResponse Build(
        IReadOnlyList<CategoryRow> rows, IReadOnlyList<CategoryRule> rules, DateOnly month, IReadOnlyList<string>? hidden = null)
    {
        var names = CategoryNames.Available(rules.Select(r => r.Category), hidden);
        bool IsInMonth(CategoryRow row) => row.BookedOn.Year == month.Year && row.BookedOn.Month == month.Month;

        // Amounts are never converted, so the breakdown follows the currency the month mostly used.
        var currency = rows
            .Where(IsInMonth)
            .GroupBy(r => r.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();

        // The same idea of "spending" as the Insights overview: money going out, own transfers aside.
        var spending = InternalTransfers
            .Without(
                rows.Where(r => r.Currency == currency).ToList(),
                r => r.AccountId, r => r.BookedOn, r => r.Amount, r => r.MarkedAsTransfer)
            .Where(r => IsInMonth(r) && r.Amount < 0)
            .ToList();

        var matcher = new CategoryMatcher(rules);
        var byCategory = spending
            .GroupBy(r => matcher.Find(r.Counterparty, r.Description)?.Category)
            .ToList();

        return new CategoriesResponse(
            currency,
            month,
            byCategory
                .Where(g => g.Key is not null)
                .Select(g => new CategorySpendingResponse(g.Key!, g.Sum(r => -r.Amount), g.Count()))
                .OrderByDescending(c => c.Amount)
                .ThenBy(c => c.Name)
                .ToList(),
            Uncategorized: byCategory.Where(g => g.Key is null).SelectMany(g => g).Sum(r => -r.Amount),
            names,
            // Two members may have a rule for the same text; it is still one payee.
            rules
                .GroupBy(r => r.Category, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(r => r.MatchKey).Distinct().Count(), StringComparer.OrdinalIgnoreCase));
    }
}
