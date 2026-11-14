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

    /// <summary>The kind ticked in the open filter panel, applied with the rest on "Show".</summary>
    private string? _stagedKind = AccountKinds.Current;

    /// <summary>Only payments in these categories; empty for no category filter.</summary>
    private HashSet<string> _categories = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Also, or only, payments with no category.</summary>
    private bool _uncategorized;

    /// <summary>Leaves out the rows labelled "Between your accounts".</summary>
    private bool _hideTransfers;

    /// <summary>Only these days; null for all of them.</summary>
    private DatePeriod? _period;

    /// <summary>The dates chip ticked in the open panel: all, this or last month, or picked on the grid.</summary>
    private string _stagedDates = AllDates;

    private const string AllDates = "all";
    private const string ThisMonth = "this";
    private const string LastMonth = "last";
    private const string PickedDates = "pick";

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

    /// <summary>What an investment account holds; empty for bank accounts and the household tab.</summary>
    public ObservableCollection<HoldingItem> Holdings { get; } = [];

    [ObservableProperty]
    public partial bool HasHoldings { get; set; }

    /// <summary>Everyday, Savings, Investments, All: which accounts the household tab lists.</summary>
    public ObservableCollection<ChoiceItem> Filters { get; } = [];

    /// <summary>The types an account can be given, for the dropdown in the account view.</summary>
    public ObservableCollection<ChoiceItem> AccountTypes { get; } = [];

    public bool HasLoaded { get; private set; }

    /// <summary>
    /// Set when another page sent this one a new filter after it had loaded; the page then
    /// reloads when it comes into view instead of only refreshing its labels.
    /// </summary>
    public bool NeedsReload { get; private set; }

    // ----- Filter -----

    /// <summary>
    /// True while the filter panel is open. It sits above the list, outside it, so it can be
    /// reached at any point in the scroll.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFilterOpen { get; set; }

    /// <summary>What the list is narrowed to, in words, shown on the filter row.</summary>
    [ObservableProperty]
    public partial string? FilterSummary { get; set; }

    /// <summary>True when categories narrow the list, so the row can offer to clear them.</summary>
    [ObservableProperty]
    public partial bool HasCategoryFilter { get; set; }

    /// <summary>The categories offered in the filter panel, "No category" first.</summary>
    public ObservableCollection<CategoryFilterOption> CategoryOptions { get; } = [];

    /// <summary>The transfers switch in the open filter panel, applied with the rest on "Show".</summary>
    [ObservableProperty]
    public partial bool StagedHideTransfers { get; set; }

    /// <summary>The date choices in the filter panel, as chips.</summary>
    public ObservableCollection<ChoiceItem> DateFilters { get; } = [];

    /// <summary>The month grid for picking a first and last day, shown while that chip is ticked.</summary>
    public DateRangePicker DatePicker { get; } = new();

    [ObservableProperty]
    public partial bool IsPickingDates { get; set; }

    private static DateOnly? Date(IDictionary<string, object> query, string key) =>
        query.TryGetValue(key, out var value)
        && DateOnly.TryParseExact(value?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date)
            ? date
            : null;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("accountId", out var id) && Guid.TryParse(id?.ToString(), out var accountId))
        {
            _accountId = accountId;
            _kindFilter = null;
            IsAccountView = true;
        }

        // Sent by the category chart: that category's payments, from everyday accounts, which
        // is where the tab opens anyway; the filter row can widen it to all accounts.
        var category = query.TryGetValue("category", out var named) ? named?.ToString() : null;
        var uncategorized = query.TryGetValue("uncategorized", out var none) && none?.ToString() == "true";
        if (!string.IsNullOrEmpty(category) || uncategorized)
        {
            _categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(category))
            {
                _categories.Add(Uri.UnescapeDataString(category));
            }

            _uncategorized = uncategorized;

            // The chart's period comes along, so the list shows the payments the slice counted.
            _period = Date(query, "from") is { } from && Date(query, "to") is { } to && from <= to
                ? new DatePeriod(from, to)
                : null;
            if (!IsAccountView)
            {
                _kindFilter = AccountKinds.Current;
                _stagedKind = AccountKinds.Current;
            }

            IsFilterOpen = false;
            NeedsReload = HasLoaded;
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
        NeedsReload = false;
        var page = await GetPageAsync(0, PageSize);
        var holdings = _accountId is { } accountId ? await _api.GetHoldingsAsync(accountId) : [];

        Groups.Clear();
        _loaded = 0;
        Append(page);

        Holdings.Clear();
        foreach (var holding in holdings)
        {
            Holdings.Add(HoldingItem.From(holding));
        }

        HasHoldings = Holdings.Count > 0;
        HasLoaded = true;
        IsEmpty = Groups.Count == 0 && !HasHoldings;
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
            Append(await GetPageAsync(_loaded, PageSize));
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

    /// <summary>
    /// Opens the filter panel with what is applied now ticked, or closes it without
    /// changing anything. The categories are read afresh each time, so new ones show.
    /// </summary>
    [RelayCommand]
    private async Task ToggleFilterAsync()
    {
        if (IsFilterOpen)
        {
            IsFilterOpen = false;
            return;
        }

        _stagedKind = _kindFilter;
        StagedHideTransfers = _hideTransfers;
        _stagedDates = DatesChoiceOf(_period);
        IsPickingDates = _stagedDates == PickedDates;
        DatePicker.Begin(_period);
        RebuildChoices();
        IsFilterOpen = true;
        await RunAsync(async () =>
        {
            var names = (await _api.GetCategoriesAsync(null)).Names;

            // A category asked for that is no longer on offer is still shown, so it can be unticked.
            var shown = names.Concat(_categories.Where(c => !names.Contains(c, StringComparer.OrdinalIgnoreCase)));
            CategoryOptions.Clear();
            CategoryOptions.Add(new CategoryFilterOption(null, _uncategorized));
            foreach (var name in shown)
            {
                CategoryOptions.Add(new CategoryFilterOption(name, _categories.Contains(name)));
            }
        });
    }

    /// <summary>Ticks a kind of account in the open panel; it takes effect on "Show".</summary>
    [RelayCommand]
    private void SelectFilter(ChoiceItem filter)
    {
        _stagedKind = filter.Kind;
        RebuildChoices();
    }

    /// <summary>Ticks or unticks a category in the open panel; any number can be ticked.</summary>
    [RelayCommand]
    private static void ToggleCategory(CategoryFilterOption option) => option.IsSelected = !option.IsSelected;

    /// <summary>Applies what is ticked in the panel and lists from the newest again.</summary>
    [RelayCommand]
    private async Task ApplyFilterAsync()
    {
        // Picked dates must be a whole range; the grid says what is missing.
        if (_stagedDates == PickedDates && DatePicker.Range is null)
        {
            return;
        }

        _period = StagedPeriod();
        _kindFilter = IsAccountView ? null : _stagedKind;
        _categories = CategoryOptions
            .Where(o => o is { IsSelected: true, Name: not null })
            .Select(o => o.Name!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        _uncategorized = CategoryOptions.Any(o => o is { IsSelected: true, IsNoCategory: true });
        _hideTransfers = StagedHideTransfers;
        IsFilterOpen = false;
        RebuildChoices();
        await LoadAsync();
    }

    /// <summary>Ticks a date choice in the open panel; "Pick dates" opens the month grid.</summary>
    [RelayCommand]
    private void SelectDates(ChoiceItem choice)
    {
        _stagedDates = choice.Kind ?? AllDates;
        IsPickingDates = _stagedDates == PickedDates;
        RebuildChoices();
    }

    /// <summary>Which chip stands for a range: a calendar month this month or last, otherwise picked days.</summary>
    private static string DatesChoiceOf(DatePeriod? period)
    {
        var thisMonth = DatePeriod.MonthOf(DateOnly.FromDateTime(DateTime.UtcNow));
        return period switch
        {
            null => AllDates,
            _ when period == thisMonth => ThisMonth,
            _ when period == thisMonth.Shift(-1) => LastMonth,
            _ => PickedDates,
        };
    }

    private DatePeriod? StagedPeriod()
    {
        var thisMonth = DatePeriod.MonthOf(DateOnly.FromDateTime(DateTime.UtcNow));
        return _stagedDates switch
        {
            ThisMonth => thisMonth,
            LastMonth => thisMonth.Shift(-1),
            PickedDates => DatePicker.Range,
            _ => null,
        };
    }

    /// <summary>Unticks every category in the open panel; the kind of account stays.</summary>
    [RelayCommand]
    private void ClearCategories()
    {
        foreach (var option in CategoryOptions)
        {
            option.IsSelected = false;
        }
    }

    /// <summary>Drops the category filter straight from the filter row.</summary>
    [RelayCommand]
    private async Task RemoveCategoryFilterAsync()
    {
        _categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _uncategorized = false;
        IsFilterOpen = false;
        RebuildChoices();
        await LoadAsync();
    }

    /// <summary>
    /// What tapping the Transactions tab while already on it does, besides scrolling to the
    /// top: put away whatever is open.
    /// </summary>
    public void CloseOpenParts()
    {
        IsFilterOpen = false;
        IsTypeMenuOpen = false;
        foreach (var item in Groups.SelectMany(group => group))
        {
            item.IsMenuOpen = false;
        }
    }

    /// <summary>"Groceries, Eating out", or "4 categories" once the names would not fit; null without a category filter.</summary>
    private string? DescribeCategories()
    {
        var names = _categories.Order(StringComparer.CurrentCultureIgnoreCase).ToList();
        if (_uncategorized)
        {
            names.Add(CategoryFilterOption.NoCategoryLabel);
        }

        return names.Count switch
        {
            0 => null,
            <= 2 => string.Join(", ", names),
            var count => $"{count} categories",
        };
    }

    private Task<List<TransactionResponse>> GetPageAsync(int skip, int limit) =>
        _api.GetTransactionsAsync(_accountId, _kindFilter, skip, limit, _categories, _uncategorized, _hideTransfers, _period);

    /// <summary>Back to Home from one account's transactions.</summary>
    [RelayCommand]
    private static Task GoBackAsync() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private void ToggleTypeMenu() => IsTypeMenuOpen = !IsTypeMenuOpen;

    /// <summary>Opens a row in place, closing any other; tapping an open row closes it.</summary>
    [RelayCommand]
    private void ToggleMenu(TransactionItem item)
    {
        var open = !item.IsMenuOpen;
        foreach (var other in Groups.SelectMany(group => group))
        {
            other.IsMenuOpen = false;
        }

        item.IsMenuOpen = open;
    }

    [RelayCommand]
    private Task MarkSubscriptionAsync(TransactionItem item) => MarkAsync(item, RecurringKinds.Subscription);

    [RelayCommand]
    private Task MarkBillAsync(TransactionItem item) => MarkAsync(item, RecurringKinds.Bill);

    /// <summary>
    /// Marks the payee as an account of the household's own that the app cannot pair up, so
    /// money sent there stops counting as spending.
    /// </summary>
    [RelayCommand]
    private Task MarkTransferAsync(TransactionItem item) => MarkAsync(item, RecurringKinds.Transfer);

    /// <summary>Stops tracking the payee of this payment.</summary>
    [RelayCommand]
    private Task StopTrackingAsync(TransactionItem item) => RunAsync(async () =>
    {
        item.IsMenuOpen = false;
        if (item.RecurringId is { } recurringId)
        {
            await _api.RemoveRecurringAsync(recurringId);
            await RefreshRecurringAsync();
        }
    });

    /// <summary>
    /// Opens the page for choosing the payment's category. The list of categories can be
    /// long, which is why this one choice is not made in the row.
    /// </summary>
    [RelayCommand]
    private static Task OpenCategoryAsync(TransactionItem item) =>
        Shell.Current.GoToAsync(
            $"{Routes.CategoryPicker}?transactionId={item.Id}"
            + $"&payee={Uri.EscapeDataString(item.Title)}"
            + $"&category={Uri.EscapeDataString(item.Category ?? "")}"
            + $"&ruleId={item.CategoryRuleId}"
            + $"&match={Uri.EscapeDataString(item.CategoryMatch ?? "")}");

    /// <summary>
    /// Brings the rows on screen up to date with what was marked elsewhere: a category
    /// chosen on the picker page, or tracking stopped from Insights. Called when the page
    /// comes back into view.
    /// </summary>
    [RelayCommand]
    private Task RefreshMarksAsync() => HasLoaded ? RunAsync(RefreshRecurringAsync) : Task.CompletedTask;

    private Task MarkAsync(TransactionItem item, string kind) => RunAsync(async () =>
    {
        if (await _api.MarkRecurringAsync(item.Id, kind, item.MatchText))
        {
            item.IsMenuOpen = false;
            await RefreshRecurringAsync();
        }
        else
        {
            Error = "That didn't work. The text to match must be part of this payment and at least 3 characters long.";
        }
    });

    /// <summary>
    /// Marking one payment changes every row to the same payee, and only the backend knows
    /// which those are. Re-reads the rows already on screen and updates them where they
    /// stand, so the list keeps its place.
    /// </summary>
    private async Task RefreshRecurringAsync()
    {
        const int batch = 200;
        var fresh = new Dictionary<Guid, TransactionResponse>();
        for (var skip = 0; skip < _loaded; skip += batch)
        {
            foreach (var transaction in await GetPageAsync(skip, batch))
            {
                fresh[transaction.Id] = transaction;
            }
        }

        foreach (var item in Groups.SelectMany(group => group))
        {
            if (fresh.TryGetValue(item.Id, out var transaction))
            {
                item.SetRecurring(transaction.RecurringId, transaction.RecurringKind, transaction.RecurringMatch);
                item.SetCategory(transaction.Category, transaction.CategoryRuleId, transaction.CategoryMatch);
            }
        }
    }

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
        // The chips in the filter panel show what is ticked there, not yet what is applied.
        Filters.Clear();
        foreach (var kind in AccountKinds.All)
        {
            Filters.Add(new ChoiceItem(AccountKinds.Label(kind), kind, kind == _stagedKind));
        }

        Filters.Add(new ChoiceItem("All", null, _stagedKind is null));

        DateFilters.Clear();
        DateFilters.Add(new ChoiceItem("All dates", AllDates, _stagedDates == AllDates));
        DateFilters.Add(new ChoiceItem("This month", ThisMonth, _stagedDates == ThisMonth));
        DateFilters.Add(new ChoiceItem("Last month", LastMonth, _stagedDates == LastMonth));
        DateFilters.Add(new ChoiceItem("Pick dates", PickedDates, _stagedDates == PickedDates));

        string?[] parts =
        [
            IsAccountView ? null : _kindFilter is null ? "All accounts" : AccountKinds.Label(_kindFilter),
            DescribeCategories(),
            _period is null ? null : PeriodText.Describe(_period),
            _hideTransfers ? "No transfers" : null,
        ];
        FilterSummary = string.Join(" · ", parts.Where(p => p is not null)) is { Length: > 0 } summary
            ? summary
            : "All payments";
        HasCategoryFilter = _categories.Count > 0 || _uncategorized;

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
            TransactionGroup? group;
            if (transaction.IsPending)
            {
                // Not yet booked: one group of its own above the days.
                group = Groups.FirstOrDefault(g => g.IsPending);
                if (group is null)
                {
                    group = new TransactionGroup(transaction.BookedOn, isPending: true);
                    Groups.Insert(0, group);
                }
            }
            else
            {
                group = Groups.LastOrDefault();
                if (group is null || group.IsPending || group.Date != transaction.BookedOn)
                {
                    group = new TransactionGroup(transaction.BookedOn);
                    Groups.Add(group);
                }
            }

            group.Add(TransactionItem.From(transaction, showAccount: _accountId is null));
        }

        // The backend sends pending ones on the first page on top of a full page of booked
        // ones, so only booked ones count towards paging.
        var booked = page.Count(t => !t.IsPending);
        _loaded += booked;
        _hasMore = booked == PageSize;
    }
}
