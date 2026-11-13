namespace Joint.Contracts;

/// <param name="TransactionId">A payment to the payee to categorize.</param>
/// <param name="Category">The category's name; a new name creates the category.</param>
/// <param name="MatchText">
/// Every payment whose name (counterparty, or description when there is none) contains this gets the category. Leave
/// it out to use the suggestion the transaction came with.
/// </param>
public record AssignCategoryRequest(Guid TransactionId, string Category, string? MatchText = null);
