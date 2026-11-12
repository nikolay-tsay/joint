namespace Joint.Api.Domain;

public class Account
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }

    /// <summary>The provider's own identifier for the account, used to match on re-sync.</summary>
    public required string ExternalId { get; set; }
    public required string Name { get; set; }

    /// <summary>The bank or broker holding the account, when the provider is an aggregator.</summary>
    public string? Institution { get; set; }
    public AccountKind Kind { get; set; }
    public required string Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountVisibility Visibility { get; set; } = AccountVisibility.Full;
}
