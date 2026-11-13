namespace Joint.Api.Domain;

/// <summary>
/// A payee someone marked as a subscription or a regular payment. Every outgoing
/// transaction to the same payee counts towards it, past and future. It belongs to the
/// person who marked it and is seen by their whole household, so it follows them if they
/// change household, the way connections do.
/// </summary>
public class RecurringPayment
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public RecurringKind Kind { get; set; }

    /// <summary>The payee as shown to the user, taken from the transaction that was marked.</summary>
    public required string Name { get; set; }

    /// <summary>What transactions are matched on; see <c>PaymentKey</c>.</summary>
    public required string MatchKey { get; set; }
}
