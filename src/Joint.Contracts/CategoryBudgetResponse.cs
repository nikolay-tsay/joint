namespace Joint.Contracts;

/// <summary>One category's share of the monthly budget and what has gone to it so far this month.</summary>
/// <param name="Name">The category.</param>
/// <param name="Limit">What is set aside for it per month.</param>
/// <param name="Spent">Everyday spending in it this month: tracked subscriptions and regular payments are left out, as from the whole budget.</param>
public record CategoryBudgetResponse(string Name, decimal Limit, decimal Spent);
