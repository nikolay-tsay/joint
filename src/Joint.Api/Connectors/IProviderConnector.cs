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

    /// <param name="credential">The decrypted provider credential.</param>
    /// <param name="since">Fetch transactions booked on or after this date; null for everything available.</param>
    /// <exception cref="ProviderAuthenticationException">The provider no longer accepts the credential.</exception>
    Task<ProviderSnapshot> FetchAsync(string credential, DateOnly? since, CancellationToken cancellationToken);
}
