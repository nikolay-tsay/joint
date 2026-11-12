using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Home;

/// <summary>The overview: what the household has in total, and each account.</summary>
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

    public ObservableCollection<AccountItem> Accounts { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var me = await api.GetMeAsync();
        Greeting = $"Hello, {me.DisplayName}";

        var accounts = await api.GetAccountsAsync();
        Accounts.Clear();
        foreach (var account in accounts)
        {
            Accounts.Add(AccountItem.From(account));
        }

        HasNoAccounts = accounts.Count == 0;

        var totals = accounts
            .GroupBy(a => a.Currency)
            .OrderByDescending(g => g.Count())
            .Select(g => Money.Format(g.Sum(a => a.Balance), g.Key))
            .ToList();
        Total = totals.FirstOrDefault() ?? Money.Format(0, "");
        OtherTotals = totals.Count > 1 ? string.Join("  ·  ", totals.Skip(1)) : null;
    });

    [RelayCommand]
    private Task OpenAccountAsync(AccountItem account) =>
        Shell.Current.GoToAsync(
            $"{Routes.AccountTransactions}?accountId={account.Id}&title={Uri.EscapeDataString(account.Title)}");
}
