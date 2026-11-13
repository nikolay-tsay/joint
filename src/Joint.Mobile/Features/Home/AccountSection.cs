namespace Joint.Mobile.Features.Home;

/// <summary>A tile on Home: one kind of account (everyday, savings, investments) with its total.</summary>
/// <param name="Total">The tile's balance, one figure per currency.</param>
/// <param name="ProfitLoss">
/// What the tile's holdings have gained or lost in total, one signed figure per currency;
/// null when none of its accounts holds anything.
/// </param>
/// <param name="IsLoss">True when the holdings are worth less than was paid for them.</param>
public record AccountSection(
    string Title, string Total, IReadOnlyList<AccountItem> Accounts, string? ProfitLoss = null, bool IsLoss = false)
{
    public bool HasProfitLoss => ProfitLoss is not null;
}
