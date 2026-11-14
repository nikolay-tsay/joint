namespace Joint.Contracts;

/// <summary>
/// One calendar month's spending split by category, for the whole household. Amounts are
/// positive numbers in one currency; transfers between the household's own accounts are left out.
/// </summary>
/// <param name="Currency">The currency most of the month's transactions are in; null when there are none.</param>
/// <param name="Month">The first day of the month.</param>
/// <param name="Spending">Categories with spending in the month, biggest first.</param>
/// <param name="Uncategorized">Spending that no category rule matches.</param>
/// <param name="Names">Every category that can be chosen: the starter list, then the household's own.</param>
/// <param name="Payees">
/// How many payees each category has, by name; a category with none is left out. A category
/// with payees can be removed as a whole.
/// </param>
public record CategoriesResponse(
    string? Currency,
    DateOnly Month,
    List<CategorySpendingResponse> Spending,
    decimal Uncategorized,
    List<string> Names,
    Dictionary<string, int>? Payees = null);
