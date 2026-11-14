namespace Joint.Contracts;

/// <summary>Money in and money out over one period. Both are positive numbers.</summary>
/// <param name="Month">The period's first day; for a calendar month, the 1st.</param>
/// <param name="End">The period's last day.</param>
public record MonthFlowResponse(DateOnly Month, decimal Income, decimal Spending, DateOnly? End = null);
