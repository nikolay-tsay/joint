using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Budget;

/// <summary>One saving or investment goal, formatted for display, with what its form needs to edit it.</summary>
/// <param name="Kind">Saving or Investment.</param>
/// <param name="Progress">Reached against the target, 0 to 1, for the bar.</param>
/// <param name="Percent">The same as a rounded percentage.</param>
/// <param name="SavedOf">"15 000,00 of 60 000,00 CZK".</param>
/// <param name="Detail">What is still missing, and where the figure comes from.</param>
public record GoalItem(
    Guid Id,
    string Kind,
    string Name,
    double Progress,
    string Percent,
    string SavedOf,
    string Detail,
    decimal TargetAmount,
    decimal Saved,
    Guid? AccountId)
{
    public static GoalItem From(GoalResponse goal)
    {
        var share = goal.TargetAmount > 0 ? goal.Saved / goal.TargetAmount : 0;
        var missing = goal.TargetAmount - goal.Saved;
        var source = goal.AccountName is { } account ? $"follows {account}" : "kept by hand";

        return new GoalItem(
            goal.Id,
            goal.Kind,
            goal.Name,
            (double)Math.Clamp(share, 0, 1),
            $"{Math.Max(share, 0):P0}",
            $"{Money.Format(goal.Saved, goal.Currency)} of {Money.Format(goal.TargetAmount, goal.Currency)}",
            missing > 0 ? $"{Money.Format(missing, goal.Currency)} to go · {source}" : $"Reached · {source}",
            goal.TargetAmount,
            goal.Saved,
            goal.AccountId);
    }
}
