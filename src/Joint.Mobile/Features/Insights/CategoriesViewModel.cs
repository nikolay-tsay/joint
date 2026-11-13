using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// One month's spending by category: a ring with a slice per category, and the same as a
/// list. The month can be stepped back and forward, never past the current one.
/// </summary>
public partial class CategoriesViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    private const string Hint = "Touch a slice to see its amount.";
    private const string UncategorizedName = "Uncategorized";

    private DateOnly _month = ThisMonth();

    /// <summary>"November 2026".</summary>
    [ObservableProperty]
    public partial string? MonthTitle { get; set; }

    /// <summary>False on the current month: there is nothing later to show.</summary>
    [ObservableProperty]
    public partial bool CanGoForward { get; set; }

    [ObservableProperty]
    public partial string? Total { get; set; }

    [ObservableProperty]
    public partial bool HasSpending { get; set; }

    [ObservableProperty]
    public partial bool HasNoSpending { get; set; }

    [ObservableProperty]
    public partial IDrawable? Chart { get; set; }

    /// <summary>The exact figures of the slice last touched.</summary>
    [ObservableProperty]
    public partial string Detail { get; set; } = Hint;

    /// <summary>Says how much of the month is still without a category, when any is.</summary>
    [ObservableProperty]
    public partial string? UncategorizedNote { get; set; }

    public ObservableCollection<CategoryItem> Items { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () => Show(await api.GetCategoriesAsync(_month)));

    [RelayCommand]
    private Task PreviousMonthAsync() => GoToAsync(_month.AddMonths(-1));

    [RelayCommand]
    private Task NextMonthAsync() => _month < ThisMonth() ? GoToAsync(_month.AddMonths(1)) : Task.CompletedTask;

    private Task GoToAsync(DateOnly month) => RunAsync(async () =>
    {
        var categories = await api.GetCategoriesAsync(month);
        _month = month;
        Show(categories);
    });

    private void Show(CategoriesResponse categories)
    {
        var currency = categories.Currency ?? "";
        MonthTitle = categories.Month.ToString("MMMM yyyy");
        CanGoForward = _month < ThisMonth();

        // The legend and the ring share one list: the categories, then what has none.
        var slices = categories.Spending
            .Select((category, index) => (category.Name, category.Amount, Color: ChartStyle.Palette[index % ChartStyle.Palette.Count]))
            .ToList();
        if (categories.Uncategorized > 0)
        {
            slices.Add((UncategorizedName, categories.Uncategorized, ChartStyle.Unassigned));
        }

        var total = slices.Sum(s => s.Amount);
        Total = Money.Format(total, currency);
        HasSpending = total > 0;
        HasNoSpending = !HasSpending;
        Detail = Hint;
        UncategorizedNote = categories.Uncategorized > 0
            ? $"{Money.Format(categories.Uncategorized, currency)} has no category yet. "
                + "Open a payment in Transactions and tap Category to give its payee one."
            : null;

        Items.Clear();
        foreach (var slice in slices)
        {
            Items.Add(new CategoryItem(slice.Name, Money.Format(slice.Amount, currency), Share(slice.Amount, total), slice.Color));
        }

        Chart = new PieChart(slices.Select(s => new ChartSeries([s.Amount], s.Color)).ToList())
        {
            Selected = index =>
                Detail = $"{slices[index].Name}: {Money.Format(slices[index].Amount, currency)}, "
                    + $"{Share(slices[index].Amount, total)} of the month's spending",
        };
    }

    private static string Share(decimal amount, decimal total) => total > 0 ? $"{amount / total:P0}" : "";

    private static DateOnly ThisMonth()
    {
        var today = DateTime.UtcNow;
        return new DateOnly(today.Year, today.Month, 1);
    }
}
