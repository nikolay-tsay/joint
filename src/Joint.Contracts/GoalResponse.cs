namespace Joint.Contracts;

/// <summary>A saving or investment goal and how far along it is.</summary>
/// <param name="Saved">The followed account's balance, or the figure kept by hand.</param>
/// <param name="AccountId">The account the goal follows; null when it is kept by hand.</param>
/// <param name="Kind">Saving or Investment.</param>
public record GoalResponse(
    Guid Id,
    string Name,
    decimal TargetAmount,
    decimal Saved,
    string Currency,
    Guid? AccountId,
    string? AccountName,
    string Kind = "Saving");
