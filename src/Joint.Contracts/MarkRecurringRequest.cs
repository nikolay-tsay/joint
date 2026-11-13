namespace Joint.Contracts;

/// <param name="TransactionId">A payment to the payee to track.</param>
/// <param name="Kind">Subscription or Bill.</param>
public record MarkRecurringRequest(Guid TransactionId, string Kind);
