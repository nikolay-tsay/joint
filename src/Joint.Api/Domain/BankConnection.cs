namespace Joint.Api.Domain;

public class BankConnection
{
    public Guid Id { get; set; }

    /// <summary>
    /// The person whose credential this is. Connections follow their owner, so the
    /// household sees them only while the owner is a member.
    /// </summary>
    public Guid OwnerUserId { get; set; }
    public ProviderKind Provider { get; set; }
    public ConnectionStatus Status { get; set; }

    /// <summary>Provider credential (API token or session), encrypted before it is stored.</summary>
    public required string EncryptedCredential { get; set; }

    /// <summary>
    /// For a provider whose credential covers one account (Fio): that account's id at the
    /// provider, so a person can have a connection per account and a new token finds the
    /// connection it belongs to. Null for providers whose credential covers everything.
    /// </summary>
    public string? AccountKey { get; set; }

    public DateTimeOffset? ConsentExpiresAt { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
    public string? LastError { get; set; }

    public List<Account> Accounts { get; } = [];
}
