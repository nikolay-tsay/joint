namespace Joint.Contracts;

/// <summary>What went to one category in a month, as a positive number, and in how many payments.</summary>
public record CategorySpendingResponse(string Name, decimal Amount, int Count);
