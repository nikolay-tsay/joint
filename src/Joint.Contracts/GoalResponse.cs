namespace Joint.Contracts;

/// <summary>A saving or investment goal and how far along it is.</summary>
/// <param name="Saved">
/// The combined balance of the household's accounts of the matching type: Savings for a
/// saving goal, Investment for an investment goal. Goals of one kind share this figure.
/// </param>
/// <param name="Kind">Saving or Investment.</param>
public record GoalResponse(Guid Id, string Name, decimal TargetAmount, decimal Saved, string Currency, string Kind);
