namespace Joint.Contracts;

/// <param name="Limit">What the household allows itself to spend per month; null removes the budget.</param>
public record SetBudgetRequest(decimal? Limit);
