using Joint.Api.Connectors;
using Joint.Api.Domain;

namespace Joint.Api.Tests;

public class ConnectorRegistryTests
{
    [Fact]
    public void TryGet_returns_the_connector_registered_for_the_provider()
    {
        var fio = new FakeConnector(ProviderKind.Fio);
        var registry = new ConnectorRegistry([fio, new FakeConnector(ProviderKind.Trading212)]);

        Assert.True(registry.TryGet(ProviderKind.Fio, out var found));
        Assert.Same(fio, found);
    }

    [Fact]
    public void TryGet_returns_false_when_no_connector_is_registered()
    {
        var registry = new ConnectorRegistry([new FakeConnector(ProviderKind.Fio)]);

        Assert.False(registry.TryGet(ProviderKind.AirBank, out _));
    }

    [Fact]
    public void Constructor_rejects_two_connectors_for_the_same_provider()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new ConnectorRegistry([new FakeConnector(ProviderKind.Fio), new FakeConnector(ProviderKind.Fio)]));
    }

    private sealed class FakeConnector(ProviderKind provider) : IProviderConnector
    {
        public ProviderKind Provider => provider;

        public async Task<ProviderSnapshot> FetchAsync(
            string credential, DateOnly since, CancellationToken cancellationToken)
        {
            await Task.Yield();
            return new ProviderSnapshot([]);
        }
    }
}
