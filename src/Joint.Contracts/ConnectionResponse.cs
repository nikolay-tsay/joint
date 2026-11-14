namespace Joint.Contracts;

/// <summary>A link to a provider, owned by one household member.</summary>
/// <param name="Status">Pending, Active, Failing or ConsentExpired.</param>
/// <param name="IsMine">True when the caller owns it and may remove it.</param>
/// <param name="LastError">Why the last sync failed, in words fit to show the user.</param>
/// <param name="AccountNames">
/// The accounts it brought, by name; empty until the first sync. A person can have several
/// connections to a provider whose credential is for one account, and this tells them apart.
/// </param>
public record ConnectionResponse(
    Guid Id,
    string Provider,
    string Status,
    string OwnerDisplayName,
    bool IsMine,
    DateTimeOffset? LastSyncedAt,
    string? LastError,
    List<string>? AccountNames = null);
