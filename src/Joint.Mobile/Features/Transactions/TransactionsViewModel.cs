using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Transactions;

/// <summary>
/// Transactions by day, newest first. As a tab it shows the whole household; opened
/// from an account on Home it shows that account only.
/// </summary>
public partial class TransactionsViewModel(ApiClient api, AuthService auth)
    : PageViewModel(auth), IQueryAttributable
{
    private const int PageSize = 50;

    private Guid? _accountId;
    private int _loaded;
    private bool _hasMore = true;
    private bool _isLoadingMore;

    [ObservableProperty]
    public partial string Title { get; set; } = "Transactions";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public ObservableCollection<TransactionGroup> Groups { get; } = [];

    public bool HasLoaded { get; private set; }

    /// <summary>True when showing one account, where its type can be changed.</summary>
    public bool IsAccountView => _accountId is not null;

    private string _kind = AccountKinds.Current;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("accountId", out var id) && Guid.TryParse(id?.ToString(), out var accountId))
        {
            _accountId = accountId;
        }

        if (query.TryGetValue("kind", out var kind) && kind?.ToString() is { Length: > 0 } kindText)
        {
            _kind = kindText;
        }

        if (query.TryGetValue("title", out var title) && title?.ToString() is { Length: > 0 } text)
        {
            Title = Uri.UnescapeDataString(text);
        }
    }

    /// <summary>Starts over from the newest transaction.</summary>
    [RelayCommand]
    private Task LoadAsync() => RunAsync(async () =>
    {
        var page = await api.GetTransactionsAsync(_accountId, 0, PageSize);
        Groups.Clear();
        _loaded = 0;
        Append(page);
        HasLoaded = true;
        IsEmpty = Groups.Count == 0;
    });

    /// <summary>Lets the user file this account under Everyday, Savings or Investments.</summary>
    [RelayCommand]
    private async Task ChangeTypeAsync()
    {
        if (_accountId is not { } accountId)
        {
            return;
        }

        var picked = await Shell.Current.DisplayActionSheetAsync(
            $"Account type (now {AccountKinds.Label(_kind)})",
            "Cancel",
            null,
            AccountKinds.All.Select(AccountKinds.Label).ToArray());
        if (AccountKinds.FromLabel(picked) is not { } kind || kind == _kind)
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await api.SetAccountKindAsync(accountId, kind))
            {
                _kind = kind;
            }
            else
            {
                Error = "This account is no longer available.";
            }
        });
    }

    /// <summary>Fetches the next page when the list is scrolled near its end.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || _isLoadingMore || IsBusy)
        {
            return;
        }

        _isLoadingMore = true;
        try
        {
            Append(await api.GetTransactionsAsync(_accountId, _loaded, PageSize));
        }
        catch (Exception exception) when (exception is HttpRequestException or SignedOutException)
        {
            // Scrolling further or pulling to refresh will try again.
        }
        finally
        {
            _isLoadingMore = false;
        }
    }

    private void Append(List<Joint.Contracts.TransactionResponse> page)
    {
        foreach (var transaction in page)
        {
            var group = Groups.LastOrDefault();
            if (group is null || group.Date != transaction.BookedOn)
            {
                group = new TransactionGroup(transaction.BookedOn);
                Groups.Add(group);
            }

            group.Add(TransactionItem.From(transaction, showAccount: _accountId is null));
        }

        _loaded += page.Count;
        _hasMore = page.Count == PageSize;
    }
}
