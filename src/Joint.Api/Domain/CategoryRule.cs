namespace Joint.Api.Domain;

/// <summary>
/// Says which category payments to a payee belong to. Every outgoing transaction whose
/// name (the counterparty, or the description when there is none) contains the match text gets the category, past and future;
/// nothing is stored on transactions. It works like a tracked payment and is independent of
/// one: a payee can be a subscription and also have a category. A rule belongs to the person
/// who made it and applies to their whole household, so it follows them if they change household.
/// </summary>
public class CategoryRule
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>The category's name. There is no table of categories: one exists while a rule uses it.</summary>
    public required string Category { get; set; }

    /// <summary>What transactions are matched on; see <c>PaymentKey</c>.</summary>
    public required string MatchKey { get; set; }
}
