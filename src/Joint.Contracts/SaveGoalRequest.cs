namespace Joint.Contracts;

/// <summary>A goal as the user wants it, for creating one or changing one.</summary>
/// <param name="AccountId">An account whose balance is the progress; null to keep the figure by hand.</param>
/// <param name="SavedAmount">What has been put aside, when kept by hand; ignored while an account is followed.</param>
/// <param name="Kind">Saving or Investment.</param>
public record SaveGoalRequest(
    string Name, decimal TargetAmount, Guid? AccountId, decimal? SavedAmount, string Kind = "Saving");
