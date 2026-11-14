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

    /// <summary>
    /// What the household allows itself to spend per budget period (a calendar month unless
    /// <see cref="BudgetPeriodFrom"/> says otherwise), in the currency most
    /// of its transactions are in; null when no budget is set.
    /// </summary>
    public decimal? MonthlyBudget { get; set; }

    /// <summary>
    /// The first and last day of one budget period when the household chose its own, such as
    /// payday to payday; the budget then covers that period's repeats (<c>DatePeriod.RepeatHolding</c>).
    /// Both null: calendar months. Cleared with the budget.
    /// </summary>
    public DateOnly? BudgetPeriodFrom { get; set; }

    public DateOnly? BudgetPeriodTo { get; set; }

    /// <summary>
    /// Starter categories the household deleted, so they are no longer offered. A category
    /// of its own needs no entry: it exists only while a rule uses it. Giving a payee one of
    /// these names again brings it back.
    /// </summary>
    public List<string> HiddenCategories { get; set; } = [];

    public List<User> Members { get; } = [];
}
