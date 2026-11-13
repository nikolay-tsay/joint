using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Where the household's money comes from and goes: this month at a glance, income against
/// spending by month, what was left over, this month's pace against last month's, and who spent.
/// </summary>
public partial class InsightsViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    private const int Months = 6;

    /// <summary>"Saved in November", or "Overspent in November" when more went out than came in.</summary>
    [ObservableProperty]
    public partial string? SavedTitle { get; set; }

    [ObservableProperty]
    public partial string? Saved { get; set; }

    [ObservableProperty]
    public partial string? Income { get; set; }

    [ObservableProperty]
    public partial string? Spending { get; set; }

    /// <summary>This month's spending against the same day last month, in words.</summary>
    [ObservableProperty]
    public partial string? PaceSummary { get; set; }

    [ObservableProperty]
    public partial IDrawable? FlowChart { get; set; }

    [ObservableProperty]
    public partial IDrawable? SavedChart { get; set; }

    [ObservableProperty]
    public partial IDrawable? PaceChart { get; set; }

    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial bool HasNoData { get; set; }

    /// <summary>Comparing people only makes sense with more than one of them.</summary>
    [ObservableProperty]
    public partial bool HasSeveralSpenders { get; set; }

    public ObservableCollection<MemberSpendingItem> Members { get; } = [];

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var insights = await api.GetInsightsAsync(Months);
        HasData = insights.Currency is not null;
        HasNoData = !HasData;
        if (insights.Currency is not { } currency)
        {
            return;
        }

        var current = insights.Months[^1];
        var net = current.Income - current.Spending;
        SavedTitle = $"{(net < 0 ? "Overspent" : "Saved")} in {current.Month:MMMM}";
        Saved = Money.Format(Math.Abs(net), currency);
        Income = Money.Format(current.Income, currency);
        Spending = Money.Format(current.Spending, currency);
        PaceSummary = DescribePace(insights);

        var labels = insights.Months.Select(m => m.Month.ToString("MMM")).ToList();
        FlowChart = new BarChart(
            labels,
            [
                new ChartSeries(insights.Months.Select(m => m.Income).ToList(), ChartStyle.Income),
                new ChartSeries(insights.Months.Select(m => m.Spending).ToList(), ChartStyle.Spending),
            ]);
        SavedChart = new BarChart(
            labels,
            [new ChartSeries(insights.Months.Select(m => m.Income - m.Spending).ToList(), ChartStyle.Income)],
            negativeColor: ChartStyle.Negative);
        PaceChart = new LineChart(
        [
            new ChartSeries(insights.SpendingLastMonth, ChartStyle.Muted),
            new ChartSeries(insights.SpendingThisMonth, ChartStyle.Spending),
        ]);

        var biggest = insights.Members.Select(m => m.Spending).DefaultIfEmpty().Max();
        Members.Clear();
        foreach (var member in insights.Members)
        {
            Members.Add(MemberSpendingItem.From(member, biggest, currency));
        }

        HasSeveralSpenders = Members.Count > 1;
    });

    private static string DescribePace(InsightsResponse insights)
    {
        if (insights.SpendingThisMonth.Count == 0 || insights.SpendingLastMonth.Count == 0)
        {
            return "";
        }

        // The same day last month, or its last day when this month is already longer.
        var day = Math.Min(insights.SpendingThisMonth.Count, insights.SpendingLastMonth.Count) - 1;
        var now = insights.SpendingThisMonth[^1];
        var then = insights.SpendingLastMonth[day];
        if (then == 0)
        {
            return now == 0 ? "Nothing spent yet this month." : "By this day last month nothing had been spent.";
        }

        var change = (now - then) / then;
        return Math.Abs(change) < 0.01m
            ? "About the same as by this day last month."
            : $"{Math.Abs(change):P0} {(change < 0 ? "less" : "more")} than by this day last month.";
    }
}
