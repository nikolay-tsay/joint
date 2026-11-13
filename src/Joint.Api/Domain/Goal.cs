namespace Joint.Api.Domain;

/// <summary>
/// An amount the household wants to reach, in savings or in investments. Progress is not
/// stored: it is the combined balance of the household's accounts of the matching type.
/// A goal belongs to the person who made it and is seen by their whole household, so it
/// follows them if they change household, the way connections do.
/// </summary>
public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public GoalKind Kind { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
