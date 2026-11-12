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

    [ObservableProperty]
    public partial bool HasNoAccounts { get; set; }

    [ObservableProperty]
    public partial bool HasAccounts { get; set; }

    /// <summary>Everyday, Savings and Investments tiles; a kind with no accounts has no tile.</summary>
    public ObservableCollection<AccountSection> Sections { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var me = await api.GetMeAsync();
        Greeting = $"Hello, {me.DisplayName}";

        var accounts = await api.GetAccountsAsync();
        HasNoAccounts = accounts.Count == 0;
        HasAccounts = accounts.Count > 0;

        var totals = Totals(accounts);
        Total = totals.FirstOrDefault() ?? Money.Format(0, "");
        OtherTotals = totals.Count > 1 ? string.Join("  ·  ", totals.Skip(1)) : null;

        Sections.Clear();
        foreach (var kind in AccountKinds.All)
        {
            var ofKind = accounts.Where(a => AccountKinds.Label(a.Kind) == AccountKinds.Label(kind)).ToList();
            if (ofKind.Count > 0)
            {
                Sections.Add(new AccountSection(
                    AccountKinds.Label(kind),
                    string.Join("\n", Totals(ofKind)),
                    ofKind.Select(AccountItem.From).ToList()));
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
