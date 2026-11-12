namespace Joint.Api.Domain;

/// <summary>
/// The unit of sharing. Every user belongs to exactly one household and sees everything
/// in it; a "joint account" is simply a household with more than one user. A new user
/// starts alone in their own household and joins someone else's by accepting an invitation.
/// </summary>
public class Household
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<User> Members { get; } = [];
}
