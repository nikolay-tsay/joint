namespace Joint.Api.Domain;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }

    /// <summary>The provider's transaction id; unique per account, used to deduplicate.</summary>
    public required string ExternalId { get; set; }
    public DateOnly BookedOn { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public string? Counterparty { get; set; }
    public string? Description { get; set; }
}
