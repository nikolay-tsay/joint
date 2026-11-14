namespace Joint.Contracts;

/// <param name="Limit">What the household allows itself to spend per month; null removes the budget.</param>
/// <param name="Categories">
/// When given, the parts of the budget for single categories, replacing any there were; they
/// must fit within <paramref name="Limit"/>. When left out, the parts stay, shrunk in proportion
/// if the budget no longer holds them.
/// </param>
public record SetBudgetRequest(decimal? Limit, List<SetCategoryBudgetRequest>? Categories = null);
