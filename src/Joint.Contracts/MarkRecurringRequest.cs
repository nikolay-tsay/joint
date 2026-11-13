namespace Joint.Contracts;

/// <param name="TransactionId">A payment to the payee to track.</param>
/// <param name="Kind">Subscription or Bill.</param>
/// <param name="MatchText">
/// Every payment whose counterparty or description contains this is counted. Leave it out
/// to use the suggestion the transaction came with.
/// </param>
public record MarkRecurringRequest(Guid TransactionId, string Kind, string? MatchText = null);
