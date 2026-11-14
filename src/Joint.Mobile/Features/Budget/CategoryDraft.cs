namespace Joint.Mobile.Features.Budget;

/// <summary>A category's part of a budget being made, held until the budget is saved with it.</summary>
/// <param name="AmountText">The amount, formatted.</param>
public record CategoryDraft(string Name, decimal Amount, string AmountText);
