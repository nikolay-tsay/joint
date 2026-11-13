namespace Joint.Api.Connectors;

/// <summary>An open position in an investment account.</summary>
/// <param name="AveragePrice">Per unit, in the instrument's <paramref name="Currency"/>.</param>
/// <param name="CurrentPrice">Per unit, in the instrument's <paramref name="Currency"/>.</param>
/// <param name="Value">What the position is worth now, in the account's currency.</param>
/// <param name="UnrealizedProfitLoss">Gain or loss if sold now, in the account's currency.</param>
public record ProviderHolding(
    string Ticker,
    string Name,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string Currency,
    decimal Value,
    decimal UnrealizedProfitLoss);
