namespace Joint.Contracts;

/// <summary>The signed-in user and the households they belong to.</summary>
public record MeResponse(Guid UserId, IReadOnlyList<HouseholdSummary> Households);

public record HouseholdSummary(Guid Id, string Name);
