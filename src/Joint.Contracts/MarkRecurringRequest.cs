namespace Joint.Contracts;

/// <param name="TransactionId">A payment to the payee to track.</param>
/// <param name="Kind">Subscription, Bill or Transfer (money moved to an own account; not spending).</param>
/// <param name="MatchText">
/// Every payment whose name (counterparty, or description when there is none) contains this is counted. Leave it out
/// to use the suggestion the transaction came with.
/// </param>
public record MarkRecurringRequest(Guid TransactionId, string Kind, string? MatchText = null);
