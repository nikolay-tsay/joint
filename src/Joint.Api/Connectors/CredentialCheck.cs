namespace Joint.Api.Connectors;

/// <summary>What a provider said about a credential the user just entered.</summary>
/// <param name="Accepted">False when the provider rejects the credential.</param>
/// <param name="AccountKey">
/// For a provider whose credential covers exactly one account: that account's id at the
/// provider, the same value its <see cref="ProviderAccount.ExternalId"/> will carry. It
/// tells a new credential for an account already connected from one for another account.
/// Null for a provider whose credential covers everything the person has there.
/// </param>
public record CredentialCheck(bool Accepted, string? AccountKey = null);
