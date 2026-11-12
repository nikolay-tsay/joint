namespace Joint.Api.Domain;

/// <summary>An invitation for another user to move into a household.</summary>
public class HouseholdInvitation
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public Guid InvitedByUserId { get; set; }

    /// <summary>Hash of the code given to the invitee; the code itself is never stored.</summary>
    public required string CodeHash { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public Guid? AcceptedByUserId { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
}
