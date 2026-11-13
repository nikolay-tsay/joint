using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Home;

/// <summary>The overview: what the household has in total, then a tile per kind of account.</summary>
public partial class HomeViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    [ObservableProperty]
    public partial string? Greeting { get; set; }

    /// <summary>The total in the currency holding the most accounts.</summary>
    [ObservableProperty]
    public partial string? Total { get; set; }

    /// <summary>Totals in any further currencies; balances are never converted.</summary>
    [ObservableProperty]
    public partial string? OtherTotals { get; set; }

    /// <summary>
    /// How the main total splits between the household's members, biggest first. Empty
    /// for a household of one, where the split would only repeat the total.
    /// </summary>
    public ObservableCollection<OwnerTotalItem> OwnerTotals { get; } = [];

    [ObservableProperty]
    public partial bool HasOwnerTotals { get; set; }

    [ObservableProperty]
    public partial bool HasNoAccounts { get; set; }

    [ObservableProperty]
    public partial bool HasAccounts { get; set; }

    /// <summary>Everyday, Savings and Investments tiles; a kind with no accounts has no tile.</summary>
    public ObservableCollection<AccountSection> Sections { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var me = await api.GetMeAsync();
        Greeting = $"Hello, {me.DisplayName}";

        var accounts = await api.GetAccountsAsync();
        HasNoAccounts = accounts.Count == 0;
        HasAccounts = accounts.Count > 0;

        var totals = Totals(accounts);
        Total = totals.FirstOrDefault() ?? Money.Format(0, "");
        OtherTotals = totals.Count > 1 ? string.Join("  ·  ", totals.Skip(1)) : null;

        // Per person, in the same currency as the big number above it.
        var mainCurrency = accounts
            .GroupBy(a => a.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault();
        var perOwner = accounts
            .Where(a => a.Currency == mainCurrency)
            .GroupBy(a => a.OwnerDisplayName)
            .Select(g => (Name: g.Key, Total: g.Sum(a => a.Balance)))
            .OrderByDescending(o => o.Total)
            .ToList();
        OwnerTotals.Clear();
        if (perOwner.Count > 1)
        {
            foreach (var owner in perOwner)
            {
                OwnerTotals.Add(new OwnerTotalItem(owner.Name, Money.Format(owner.Total, mainCurrency ?? "")));
            }
        }

        HasOwnerTotals = OwnerTotals.Count > 0;

        Sections.Clear();
        foreach (var kind in AccountKinds.All)
        {
            var ofKind = accounts.Where(a => AccountKinds.Label(a.Kind) == AccountKinds.Label(kind)).ToList();
            if (ofKind.Count > 0)
            {
                // What the holdings in this tile have gained or lost, per currency like the total.
                var gains = ofKind
                    .Where(a => a.UnrealizedProfitLoss is not null)
                    .GroupBy(a => a.Currency)
                    .OrderByDescending(g => g.Count())
                    .Select(g => (Currency: g.Key, Amount: g.Sum(a => a.UnrealizedProfitLoss ?? 0)))
                    .ToList();

                Sections.Add(new AccountSection(
                    AccountKinds.Label(kind),
                    string.Join("\n", Totals(ofKind)),
                    ofKind.Select(AccountItem.From).ToList(),
                    gains.Count > 0 ? string.Join("\n", gains.Select(g => Money.FormatSigned(g.Amount, g.Currency))) : null,
                    IsLoss: gains.Count > 0 && gains[0].Amount < 0));
            }
        }
    });

    [RelayCommand]
    private Task OpenAccountAsync(AccountItem account) =>
        Shell.Current.GoToAsync(
            $"{Routes.AccountTransactions}?accountId={account.Id}"
            + $"&title={Uri.EscapeDataString(account.Title)}&kind={account.Kind}");

    /// <summary>One total per currency, the currency with the most accounts first.</summary>
    private static List<string> Totals(IEnumerable<AccountResponse> accounts) =>
        accounts
            .GroupBy(a => a.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => Money.Format(g.Sum(a => a.Balance), g.Key))
            .ToList();
}
