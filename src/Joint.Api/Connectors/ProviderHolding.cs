namespace Joint.Api.Connectors;

public record ProviderHolding(
    string Ticker,
    string Name,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string Currency);
