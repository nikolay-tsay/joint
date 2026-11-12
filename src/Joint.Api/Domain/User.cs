namespace Joint.Api.Domain;

/// <summary>The app's record of a person. Sign-in itself lives in Supabase Auth.</summary>
public class User
{
    /// <summary>The Supabase Auth user id (the JWT <c>sub</c> claim).</summary>
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }

    /// <summary>Link to the profile picture at the sign-in provider; the image itself is not stored.</summary>
    public string? AvatarUrl { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
