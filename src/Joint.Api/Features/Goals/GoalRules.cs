using Joint.Api.Data;
using Joint.Api.Domain;
using Joint.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Joint.Api.Features.Goals;

/// <summary>What creating and changing a goal have in common: checking the request and applying it.</summary>
public static class GoalRules
{
    public const int MaxNameLength = 100;

    /// <summary>Keeps a slip of the finger from storing something absurd.</summary>
    public const decimal Largest = 1_000_000_000_000m;

    /// <summary>
    /// Checks a request and writes it onto the goal. Nothing is written when it is refused.
    /// </summary>
    /// <returns>Null when applied, otherwise what is wrong with the request.</returns>
    public static async Task<string?> ApplyAsync(
        Goal goal, SaveGoalRequest request, Guid householdId, JointDbContext db, CancellationToken cancellationToken)
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

        var saved = request.SavedAmount ?? 0;
        if (saved is < 0 or > Largest)
        {
            return "The amount saved cannot be below zero.";
        }

        string currency;
        if (request.AccountId is { } accountId)
        {
            // Only the household's own accounts can be followed.
            var account = await db.AccountsOf(householdId).FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
            if (account is null)
            {
                return "That account is not available.";
            }

            currency = account.Currency;
        }
        else
        {
            // Kept by hand: assume the currency most of the household's accounts are in.
            currency = (await db.AccountsOf(householdId).Select(a => a.Currency).ToListAsync(cancellationToken))
                .GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? goal.Currency;
        }

        goal.Kind = kind;
        goal.Name = name;
        goal.TargetAmount = request.TargetAmount;
        goal.SavedAmount = saved;
        goal.AccountId = request.AccountId;
        goal.Currency = currency;
        return null;
    }
}
