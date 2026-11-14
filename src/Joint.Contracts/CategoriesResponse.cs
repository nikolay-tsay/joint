namespace Joint.Contracts;

/// <summary>
/// One period's spending split by category (a calendar month unless another is asked for), for the whole household. Amounts are
/// positive numbers in one currency; transfers between the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the period's transactions are in; null when there are none.</param>
/// <param name="Month">The period's first day; for a calendar month, the 1st.</param>
/// <param name="Spending">Categories with spending in the period, biggest first.</param>
/// <param name="Uncategorized">Spending that no category rule matches.</param>
/// <param name="Names">Every category that can be chosen: the starter list, then the household's own.</param>
/// <param name="Payees">
/// How many payees each category has, by name; a category with none is left out. A category
/// with payees can be removed as a whole.
/// </param>
/// <param name="To">The period's last day.</param>
public record CategoriesResponse(
    string? Currency,
    DateOnly Month,
    List<CategorySpendingResponse> Spending,
    decimal Uncategorized,
    List<string> Names,
    Dictionary<string, int>? Payees = null,
    DateOnly? To = null);
