using Joint.Api.Connectors;
using Joint.Api.Domain;

namespace Joint.Api.Tests;

/// <summary>A connector whose answers the test controls.</summary>
public sealed class FakeConnector(ProviderKind provider) : IProviderConnector
{
    public ProviderKind Provider => provider;

    /// <summary>What the next fetch returns; set <see cref="Failure"/> to make it throw instead.</summary>
    public ProviderSnapshot Snapshot { get; set; } = new([]);

    public Exception? Failure { get; set; }

    /// <summary>The <c>since</c> argument of every fetch, in order.</summary>
    public List<DateOnly?> FetchedSince { get; } = [];

    /// <summary>
    /// Makes this a provider whose credential is for one account: maps a credential to the
    /// account it is for. Left null, a credential covers everything, as at most providers.
    /// </summary>
    public Func<string, string>? AccountOf { get; set; }

    public Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<CredentialCheck> CheckCredentialAsync(string credential, CancellationToken cancellationToken) =>
        Task.FromResult(new CredentialCheck(true, AccountOf?.Invoke(credential)));

    public Task<ProviderSnapshot> FetchAsync(string credential, DateOnly? since, CancellationToken cancellationToken)
    {
        FetchedSince.Add(since);
        return Failure is null ? Task.FromResult(Snapshot) : Task.FromException<ProviderSnapshot>(Failure);
    }
}
