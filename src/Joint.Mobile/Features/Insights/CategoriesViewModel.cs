using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// A period's spending by category: a ring with a slice per category, and the same as a
/// list. The period is the one chosen above Overview and Categories (<see cref="PeriodFilter"/>).
/// </summary>
public partial class CategoriesViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    private const string Hint = "Touch a slice to see its amount, or a row below to see its payments.";
    private const string UncategorizedName = "Uncategorized";

    /// <summary>The days shown; Insights sets it from its period filter before loading.</summary>
    public DatePeriod Period { get; set; } = DatePeriod.MonthOf(DateOnly.FromDateTime(DateTime.UtcNow));

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

    /// <summary>Says how much of the period is still without a category, when any is.</summary>
    [ObservableProperty]
    public partial string? UncategorizedNote { get; set; }

    public ObservableCollection<CategoryItem> Items { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () => Show(await api.GetCategoriesAsync(Period)));

    /// <summary>
    /// Opens Transactions on that category's payments in the ring's period, from everyday
    /// accounts, the tab's usual view; the ring counts every kind of account.
    /// </summary>
    [RelayCommand]
    private Task OpenCategoryAsync(CategoryItem item)
    {
        var filter = item.IsUncategorized ? "uncategorized=true" : $"category={Uri.EscapeDataString(item.Name)}";
        var dates = string.Create(
            System.Globalization.CultureInfo.InvariantCulture, $"from={Period.From:yyyy-MM-dd}&to={Period.To:yyyy-MM-dd}");
        return Shell.Current.GoToAsync($"{Routes.Transactions}?{filter}&{dates}");
    }

    private void Show(CategoriesResponse categories)
    {
        var currency = categories.Currency ?? "";

        // The legend and the ring share one list: the categories, then what has none.
        var slices = categories.Spending
            .Select((category, index) => (category.Name, category.Amount, Color: ChartStyle.Palette[index % ChartStyle.Palette.Count], IsUncategorized: false))
            .ToList();
        if (categories.Uncategorized > 0)
        {
            slices.Add((UncategorizedName, categories.Uncategorized, ChartStyle.Unassigned, IsUncategorized: true));
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
            Items.Add(new CategoryItem(
                slice.Name, Money.Format(slice.Amount, currency), Share(slice.Amount, total), slice.Color, slice.IsUncategorized));
        }

        Chart = new PieChart(slices.Select(s => new ChartSeries([s.Amount], s.Color)).ToList())
        {
            Selected = index =>
                Detail = $"{slices[index].Name}: {Money.Format(slices[index].Amount, currency)}, "
                    + $"{Share(slices[index].Amount, total)} of the {PeriodText.Noun(Period)}'s spending",
        };
    }

    private static string Share(decimal amount, decimal total) => total > 0 ? $"{amount / total:P0}" : "";
}
