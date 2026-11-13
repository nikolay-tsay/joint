using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

/// <summary>One position in an investment account, already formatted for display.</summary>
/// <param name="Detail">How many units and what one costs now, e.g. "3.5 × 182.40 USD".</param>
/// <param name="Value">What the position is worth, in the account's currency.</param>
/// <param name="ProfitLoss">Gain or loss so far, signed.</param>
public record HoldingItem(string Name, string Detail, string Value, string ProfitLoss, bool IsGain, bool IsLoss)
{
    public static HoldingItem From(HoldingResponse holding) =>
        new(
            holding.Name,
            $"{holding.Quantity:0.####} × {Money.Format(holding.CurrentPrice, holding.PriceCurrency)}",
            Money.Format(holding.Value, holding.ValueCurrency),
            Money.FormatSigned(holding.UnrealizedProfitLoss, holding.ValueCurrency),
            holding.UnrealizedProfitLoss > 0,
            holding.UnrealizedProfitLoss < 0);
}
