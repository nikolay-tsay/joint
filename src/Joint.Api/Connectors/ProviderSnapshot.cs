namespace Joint.Api.Connectors;

/// <summary>Everything a provider returned for one connection in one fetch.</summary>
public record ProviderSnapshot(IReadOnlyList<ProviderAccount> Accounts);
