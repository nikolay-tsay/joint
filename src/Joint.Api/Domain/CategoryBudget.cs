namespace Joint.Api.Domain;

/// <summary>
/// Part of the household's monthly budget set aside for one category. The lines of a
/// household never add up to more than its <see cref="Household.MonthlyBudget"/>; what is
/// left over is "everything else". They belong to the household like the budget itself, and
/// go when the budget is removed, when the category is deleted, or with the household.
/// </summary>
public class CategoryBudget
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }

    /// <summary>The category's name, spelled as the household's categories spell it.</summary>
    public required string Category { get; set; }

    /// <summary>What the household allows itself per calendar month in that category.</summary>
    public decimal Amount { get; set; }
}
