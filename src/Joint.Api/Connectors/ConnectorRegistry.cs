using Joint.Api.Domain;

namespace Joint.Api.Connectors;

/// <summary>Looks up the connector for a provider.</summary>
public class ConnectorRegistry
{
    private readonly Dictionary<ProviderKind, IProviderConnector> _connectors;

    public ConnectorRegistry(IEnumerable<IProviderConnector> connectors)
    {
        _connectors = [];
        foreach (var connector in connectors)
        {
            if (!_connectors.TryAdd(connector.Provider, connector))
            {
                throw new InvalidOperationException(
                    $"More than one connector is registered for {connector.Provider}.");
            }
        }
    }

    public bool TryGet(ProviderKind provider, out IProviderConnector connector) =>
        _connectors.TryGetValue(provider, out connector!);
}
