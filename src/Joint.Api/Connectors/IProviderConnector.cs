using Joint.Api.Domain;

namespace Joint.Api.Connectors;

/// <summary>
/// Reads accounts, transactions and holdings from one provider (a bank, a broker or an
/// aggregator) and returns them in the app's normalized shape. Connectors are read-only.
/// </summary>
public interface IProviderConnector
{
    ProviderKind Provider { get; }

    /// <summary>Checks a credential the user just entered, without fetching their data.</summary>
    /// <returns>False when the provider rejects the credential.</returns>
    Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken);

    /// <summary>
    /// The same check, also naming the account when a credential covers exactly one. A
    /// provider like that overrides this so that a person can connect several accounts;
    /// it must still make only one request, since such providers tend to ration them.
    /// </summary>
    async Task<CredentialCheck> CheckCredentialAsync(string credential, CancellationToken cancellationToken) =>
        new(await ValidateCredentialAsync(credential, cancellationToken));

    /// <param name="credential">The decrypted provider credential.</param>
    /// <param name="since">Fetch transactions booked on or after this date; null for everything available.</param>
    /// <exception cref="ProviderAuthenticationException">The provider no longer accepts the credential.</exception>
    Task<ProviderSnapshot> FetchAsync(string credential, DateOnly? since, CancellationToken cancellationToken);
}
