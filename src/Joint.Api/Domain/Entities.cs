namespace Joint.Api.Domain;

public class Household
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public List<HouseholdMember> Members { get; } = [];
}

public class HouseholdMember
{
    public Guid Id { get; set; }
    public Guid HouseholdId { get; set; }

    /// <summary>The Supabase Auth user id (the JWT <c>sub</c> claim).</summary>
    public Guid UserId { get; set; }
    public required string DisplayName { get; set; }
}

public class BankConnection
{
    public Guid Id { get; set; }
    public Guid MemberId { get; set; }
    public ProviderKind Provider { get; set; }
    public ConnectionStatus Status { get; set; }

    /// <summary>Provider credential (API token or session), encrypted before it is stored.</summary>
    public required string EncryptedCredential { get; set; }

    public DateTimeOffset? ConsentExpiresAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }

    public List<Account> Accounts { get; } = [];
}

public class Account
{
    public Guid Id { get; set; }
    public Guid ConnectionId { get; set; }

    /// <summary>The provider's own identifier for the account, used to match on re-sync.</summary>
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public AccountKind Kind { get; set; }
    public required string Currency { get; set; }
    public decimal Balance { get; set; }
    public AccountVisibility Visibility { get; set; } = AccountVisibility.Full;
}

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

public class Holding
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string Ticker { get; set; }
    public required string Name { get; set; }
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public required string Currency { get; set; }
}
