namespace Joint.Contracts;

/// <summary>A goal as the user wants it, for creating one or changing one.</summary>
/// <param name="Kind">Saving or Investment.</param>
public record SaveGoalRequest(string Name, decimal TargetAmount, string Kind);
