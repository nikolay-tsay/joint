namespace Joint.Api.Connectors;

/// <summary>The provider rejected the stored credential; the user has to reconnect.</summary>
public class ProviderAuthenticationException(string message) : Exception(message);
