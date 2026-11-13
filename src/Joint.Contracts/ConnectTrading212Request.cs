namespace Joint.Contracts;

/// <summary>The two halves of a Trading 212 API key, as shown once when the key is created.</summary>
public record ConnectTrading212Request(string ApiKey, string ApiSecret);
