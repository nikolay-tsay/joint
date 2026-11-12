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

    public Task<bool> ValidateCredentialAsync(string credential, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<ProviderSnapshot> FetchAsync(string credential, DateOnly? since, CancellationToken cancellationToken)
    {
        FetchedSince.Add(since);
        return Failure is null ? Task.FromResult(Snapshot) : Task.FromException<ProviderSnapshot>(Failure);
    }
}
