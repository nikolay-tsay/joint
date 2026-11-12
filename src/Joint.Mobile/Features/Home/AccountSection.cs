namespace Joint.Mobile.Features.Home;

/// <summary>A tile on Home: one kind of account (everyday, savings, investments) with its total.</summary>
/// <param name="Total">The tile's balance, one figure per currency.</param>
public record AccountSection(string Title, string Total, IReadOnlyList<AccountItem> Accounts);
