using Joint.Api.Domain;

namespace Joint.Api.Features.Budget;

/// <summary>The rules that keep a household's category lines within its monthly budget.</summary>
public static class BudgetLines
{
    /// <summary>The household's spelling of a category typed in any capitals; null when it has no such category.</summary>
    public static string? CategoryNamed(IEnumerable<string> available, string? typed) =>
        available.FirstOrDefault(n => string.Equals(n, typed?.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>What the budget has not yet set aside for a category, leaving out <paramref name="except"/>.</summary>
    public static decimal Unassigned(decimal limit, IEnumerable<CategoryBudget> lines, string? except = null) =>
        limit - lines
            .Where(l => except is null || !string.Equals(l.Category, except, StringComparison.OrdinalIgnoreCase))
            .Sum(l => l.Amount);

    /// <summary>
    /// Fits the lines into a budget lowered below their sum by shrinking each in proportion,
    /// at the user's request (refusing the new budget was the other choice). Amounts are
    /// rounded down to whole hundredths so the sum never ends up above the budget.
    /// </summary>
    public static void ShrinkTo(decimal limit, IReadOnlyList<CategoryBudget> lines)
    {
        var total = lines.Sum(l => l.Amount);
        if (total <= limit || total <= 0)
        {
            return;
        }

        foreach (var line in lines)
        {
            line.Amount = Math.Floor(line.Amount * limit / total * 100) / 100;
        }
    }
}
