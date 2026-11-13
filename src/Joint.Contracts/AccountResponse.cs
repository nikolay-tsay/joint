namespace Joint.Contracts;

/// <param name="UnrealizedProfitLoss">
/// For an account with holdings: what they have gained or lost in total against what was
/// paid for them, in the account's currency. Null for an account without holdings.
/// </param>
public record AccountResponse(
    Guid Id,
    string Name,
    string? Institution,
    string Kind,
    string Currency,
    decimal Balance,
    string OwnerDisplayName,
    decimal? UnrealizedProfitLoss = null);
