namespace Joint.Contracts;

/// <summary>The signed-in user and the household they share their finances with.</summary>
public record MeResponse(Guid UserId, string Email, string DisplayName, HouseholdResponse Household);

/// <summary>A household; more than one member means the accounts are joint.</summary>
public record HouseholdResponse(Guid Id, string Name, IReadOnlyList<HouseholdMemberResponse> Members);

public record HouseholdMemberResponse(Guid UserId, string DisplayName);
