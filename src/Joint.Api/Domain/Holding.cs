namespace Joint.Api.Domain;

/// <summary>An open position in an investment account. Current state only, replaced on each sync.</summary>
public class Holding
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public required string Ticker { get; set; }
    public required string Name { get; set; }
    public decimal Quantity { get; set; }

    /// <summary>Per unit, in the instrument's <see cref="Currency"/>.</summary>
    public decimal AveragePrice { get; set; }

    /// <summary>Per unit, in the instrument's <see cref="Currency"/>.</summary>
    public decimal CurrentPrice { get; set; }

    /// <summary>The instrument's trading currency, which can differ from the account's.</summary>
    public required string Currency { get; set; }

    /// <summary>What the position is worth now, in the account's currency.</summary>
    public decimal Value { get; set; }

    /// <summary>Gain or loss if sold now, in the account's currency.</summary>
    public decimal UnrealizedProfitLoss { get; set; }
}
