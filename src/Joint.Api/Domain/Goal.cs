namespace Joint.Api.Domain;

/// <summary>
/// Something the household is saving or investing towards. Progress either follows the balance of an
/// account set aside for it, or is a figure kept by hand. A goal belongs to the person who
/// made it and is seen by their whole household, so it follows them if they change
/// household, the way connections do.
/// </summary>
public class Goal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public GoalKind Kind { get; set; }
    public required string Name { get; set; }
    public decimal TargetAmount { get; set; }

    /// <summary>What has been saved, when the goal is kept by hand; ignored while an account is followed.</summary>
    public decimal SavedAmount { get; set; }

    /// <summary>The account whose balance is the progress; null when the goal is kept by hand.</summary>
    public Guid? AccountId { get; set; }

    /// <summary>The followed account's currency, or the household's main one when kept by hand.</summary>
    public required string Currency { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
