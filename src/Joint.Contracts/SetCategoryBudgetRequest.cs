namespace Joint.Contracts;

/// <summary>Sets aside part of the monthly budget for one category, or changes what is set aside.</summary>
/// <param name="Category">One of the household's categories.</param>
/// <param name="Amount">Per month; together with the other categories it must fit within the budget.</param>
public record SetCategoryBudgetRequest(string Category, decimal Amount);
