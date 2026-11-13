namespace Joint.Contracts;

/// <summary>Money in and money out over one calendar month. Both are positive numbers.</summary>
/// <param name="Month">The first day of the month.</param>
public record MonthFlowResponse(DateOnly Month, decimal Income, decimal Spending);
