using Joint.Api.Domain;
using Joint.Contracts;

namespace Joint.Api.Features.Goals;

/// <summary>
/// What the goal endpoints have in common: checking a request and applying it, and working
/// out how far along a kind of goal is.
/// </summary>
public static class GoalRules
{
    public const int MaxNameLength = 100;

    /// <summary>Keeps a slip of the finger from storing something absurd.</summary>
    public const decimal Largest = 1_000_000_000_000m;

    /// <summary>Checks a request and writes it onto the goal. Nothing is written when it is refused.</summary>
    /// <returns>Null when applied, otherwise what is wrong with the request.</returns>
    public static string? Apply(Goal goal, SaveGoalRequest request)
    {
        // Names only: Enum.TryParse would also accept a bare number.
        if (!Enum.GetNames<GoalKind>().Contains(request.Kind, StringComparer.OrdinalIgnoreCase)
            || !Enum.TryParse<GoalKind>(request.Kind, ignoreCase: true, out var kind))
        {
            return $"Kind must be one of: {string.Join(", ", Enum.GetNames<GoalKind>())}.";
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
        {
            return $"A goal needs a name of at most {MaxNameLength} characters.";
        }

        if (request.TargetAmount is <= 0 or > Largest)
        {
            return "The target is an amount above zero.";
        }

        goal.Kind = kind;
        goal.Name = name;
        goal.TargetAmount = request.TargetAmount;
        return null;
    }

    /// <summary>
    /// What counts towards goals of a kind: the combined balance of the household's
    /// accounts of the matching type. Balances are never converted, so with several
    /// currencies only the one most of those accounts are in is added up.
    /// </summary>
    public static (decimal Reached, string Currency) Progress(GoalKind kind, IReadOnlyList<Account> accounts)
    {
        var wanted = kind == GoalKind.Investment ? AccountKind.Investment : AccountKind.Savings;
        var counted = accounts.Where(a => a.Kind == wanted).ToList();

        // No account of that type yet: still name a currency, so the target can be shown.
        var currency = MostUsedCurrency(counted) ?? MostUsedCurrency(accounts) ?? "";
        return (counted.Where(a => a.Currency == currency).Sum(a => a.Balance), currency);
    }

    private static string? MostUsedCurrency(IEnumerable<Account> accounts) =>
        accounts
            .GroupBy(a => a.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
}
