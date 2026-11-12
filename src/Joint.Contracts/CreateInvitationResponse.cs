namespace Joint.Contracts;

/// <summary>A new household invitation. The code is shown once and cannot be retrieved again.</summary>
public record CreateInvitationResponse(string Code, DateTimeOffset ExpiresAt);
