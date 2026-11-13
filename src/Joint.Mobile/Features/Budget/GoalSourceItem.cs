namespace Joint.Mobile.Features.Budget;

/// <summary>One choice of where a goal's progress comes from.</summary>
/// <param name="AccountId">The account to follow; null means the figure is kept by hand.</param>
public record GoalSourceItem(Guid? AccountId, string Label, bool IsSelected);
