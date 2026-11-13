namespace Joint.Mobile.Features.Budget;

/// <summary>
/// One of the pages the Budget carousel slides between. Both are drawn from the same view
/// model; the key only says which layout it gets.
/// </summary>
public record BudgetView(string Key, BudgetViewModel Model)
{
    public const string Budget = "Budget";
    public const string Goals = "Goals";
}
