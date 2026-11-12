using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Transactions;

/// <summary>
/// Transactions by day, newest first. As a tab it shows the household, filtered by kind of
/// account (everyday by default). Opened from an account on Home it shows that account
/// only, with a dropdown for changing the account's type.
/// </summary>
public partial class TransactionsViewModel : PageViewModel, IQueryAttributable
{
    private const int PageSize = 50;

    private readonly ApiClient _api;
    private Guid? _accountId;
    private string _accountKind = AccountKinds.Current;

    /// <summary>The household view's filter; null shows every kind of account.</summary>
    private string? _kindFilter = AccountKinds.Current;

    private int _loaded;
    private bool _hasMore = true;
    private bool _isLoadingMore;

    public TransactionsViewModel(ApiClient api, AuthService auth)
        : base(auth)
    {
        _api = api;
        RebuildChoices();
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "Transactions";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>True when showing one account, false for the household tab.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHouseholdView))]
    public partial bool IsAccountView { get; set; }

    public bool IsHouseholdView => !IsAccountView;

    /// <summary>The account's current type with a marker that it opens, e.g. "Everyday ▾".</summary>
    [ObservableProperty]
    public partial string? AccountTypeSummary { get; set; }

    [ObservableProperty]
    public partial bool IsTypeMenuOpen { get; set; }

    public ObservableCollection<TransactionGroup> Groups { get; } = [];

    /// <summary>Everyday, Savings, Investments, All: which accounts the household tab lists.</summary>
    public ObservableCollection<ChoiceItem> Filters { get; } = [];

    /// <summary>The types an account can be given, for the dropdown in the account view.</summary>
    public ObservableCollection<ChoiceItem> AccountTypes { get; } = [];

    public bool HasLoaded { get; private set; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("accountId", out var id) && Guid.TryParse(id?.ToString(), out var accountId))
        {
            _accountId = accountId;
            _kindFilter = null;
            IsAccountView = true;
        }

        if (query.TryGetValue("kind", out var kind) && kind?.ToString() is { Length: > 0 } kindText)
        {
            _accountKind = kindText;
        }

        if (query.TryGetValue("title", out var title) && title?.ToString() is { Length: > 0 } text)
        {
            Title = Uri.UnescapeDataString(text);
        }

        RebuildChoices();
    }

    /// <summary>Starts over from the newest transaction.</summary>
    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var page = await _api.GetTransactionsAsync(_accountId, _kindFilter, 0, PageSize);
        Groups.Clear();
        _loaded = 0;
        Append(page);
        HasLoaded = true;
        IsEmpty = Groups.Count == 0;
    });

    /// <summary>Fetches the next page when the list is scrolled near its end.</summary>
    [RelayCommand]
    private async Task LoadMoreAsync()
    {
        if (!_hasMore || _isLoadingMore || IsRefreshing)
        {
            return;
        }

        _isLoadingMore = true;
        try
        {
            Append(await _api.GetTransactionsAsync(_accountId, _kindFilter, _loaded, PageSize));
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

    /// <summary>Switches the household tab to another kind of account, or to all of them.</summary>
    [RelayCommand]
    private async Task SelectFilterAsync(ChoiceItem filter)
    {
        if (filter.Kind == _kindFilter)
        {
            return;
        }

        _kindFilter = filter.Kind;
        RebuildChoices();
        await LoadAsync();
    }

    [RelayCommand]
    private void ToggleTypeMenu() => IsTypeMenuOpen = !IsTypeMenuOpen;

    /// <summary>Files this account under the chosen type and closes the dropdown.</summary>
    [RelayCommand]
    private async Task SelectAccountTypeAsync(ChoiceItem type)
    {
        IsTypeMenuOpen = false;
        if (_accountId is not { } accountId || type.Kind is not { } kind || kind == _accountKind)
        {
            return;
        }

        await RunAsync(async () =>
        {
            if (await _api.SetAccountKindAsync(accountId, kind))
            {
                _accountKind = kind;
                RebuildChoices();
            }
            else
            {
                Error = "This account is no longer available.";
            }
        });
    }

    private void RebuildChoices()
    {
        Filters.Clear();
        foreach (var kind in AccountKinds.All)
        {
            Filters.Add(new ChoiceItem(AccountKinds.Label(kind), kind, kind == _kindFilter));
        }

        Filters.Add(new ChoiceItem("All", null, _kindFilter is null));

        AccountTypes.Clear();
        foreach (var kind in AccountKinds.All)
        {
            AccountTypes.Add(new ChoiceItem(AccountKinds.Label(kind), kind, kind == _accountKind));
        }

        AccountTypeSummary = $"{AccountKinds.Label(_accountKind)} ▾";
    }

    private void Append(List<TransactionResponse> page)
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
