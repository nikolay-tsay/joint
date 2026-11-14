namespace Joint.Api.Domain;

/// <summary>
/// A transaction the bank has not booked yet, such as a card payment still on hold. It is
/// only shown: no figure in the app counts it. Pending items have no id that survives
/// booking and can change or vanish, so they are kept apart from <see cref="Transaction"/>
/// and an account's are replaced wholesale on each sync, like holdings. Once booked, the
/// payment arrives as an ordinary transaction and its pending row is gone with the next sync.
/// </summary>
public class PendingTransaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>The date the provider gives it, usually when the payment was made.</summary>
    public DateOnly Date { get; set; }

    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public string? Counterparty { get; set; }
    public string? Description { get; set; }
}
