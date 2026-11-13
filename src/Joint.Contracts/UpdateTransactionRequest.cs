namespace Joint.Contracts;

/// <param name="ExcludedFromBudget">True to keep this payment out of the budget's figures.</param>
public record UpdateTransactionRequest(bool ExcludedFromBudget);
