namespace Joint.Contracts;

/// <summary>An open position in an investment account.</summary>
/// <param name="AveragePrice">Per unit, in <paramref name="PriceCurrency"/>.</param>
/// <param name="CurrentPrice">Per unit, in <paramref name="PriceCurrency"/>.</param>
/// <param name="PriceCurrency">The instrument's trading currency.</param>
/// <param name="Value">What the position is worth now, in <paramref name="ValueCurrency"/>.</param>
/// <param name="UnrealizedProfitLoss">Gain or loss if sold now, in <paramref name="ValueCurrency"/>.</param>
/// <param name="ValueCurrency">The account's currency.</param>
public record HoldingResponse(
    string Ticker,
    string Name,
    decimal Quantity,
    decimal AveragePrice,
    decimal CurrentPrice,
    string PriceCurrency,
    decimal Value,
    decimal UnrealizedProfitLoss,
    string ValueCurrency);
