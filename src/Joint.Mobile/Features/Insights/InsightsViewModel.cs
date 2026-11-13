using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Two views behind one tab. Overview: this month at a glance, income against spending by
/// month, what was left over, this month's pace against last month's, and who spent.
/// Recurring: what subscriptions and regular payments cost per month, and the list of them.
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

    // ----- Recurring view -----

    /// <summary>Which of the page's two views is showing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewTab))]
    public partial bool IsRecurringTab { get; set; }

    public bool IsOverviewTab => !IsRecurringTab;

    [ObservableProperty]
    public partial string? RecurringTitle { get; set; }

    [ObservableProperty]
    public partial string? RecurringTotal { get; set; }

    [ObservableProperty]
    public partial string? SubscriptionsTotal { get; set; }

    [ObservableProperty]
    public partial string? BillsTotal { get; set; }

    [ObservableProperty]
    public partial IDrawable? RecurringChart { get; set; }

    [ObservableProperty]
    public partial bool HasRecurring { get; set; }

    [ObservableProperty]
    public partial bool HasNoRecurring { get; set; }

    [ObservableProperty]
    public partial bool HasSubscriptions { get; set; }

    [ObservableProperty]
    public partial bool HasBills { get; set; }

    public ObservableCollection<RecurringItem> Subscriptions { get; } = [];

    public ObservableCollection<RecurringItem> Bills { get; } = [];

    [RelayCommand]
    private void ShowOverview() => IsRecurringTab = false;

    [RelayCommand]
    private void ShowRecurring() => IsRecurringTab = true;

    /// <summary>Stops tracking a payee; its transactions stay as they are.</summary>
    [RelayCommand]
    private Task RemoveRecurringAsync(RecurringItem item) => RunAsync(async () =>
    {
        await api.RemoveRecurringAsync(item.Id);
        await LoadAsync();
    });

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        var insights = await api.GetInsightsAsync(Months);
        FillRecurring(await api.GetRecurringAsync(Months), insights.Currency);

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

    private void FillRecurring(RecurringOverviewResponse recurring, string? householdCurrency)
    {
        HasRecurring = recurring.Payments.Count > 0;
        HasNoRecurring = !HasRecurring;

        var currency = recurring.Currency ?? householdCurrency ?? "";
        var current = recurring.Months[^1];
        RecurringTitle = $"Recurring in {current.Month:MMMM}";
        RecurringTotal = Money.Format(current.Subscriptions + current.Bills, currency);
        SubscriptionsTotal = Money.Format(current.Subscriptions, currency);
        BillsTotal = Money.Format(current.Bills, currency);

        RecurringChart = new BarChart(
            recurring.Months.Select(m => m.Month.ToString("MMM")).ToList(),
            [
                new ChartSeries(recurring.Months.Select(m => m.Subscriptions).ToList(), ChartStyle.Spending),
                new ChartSeries(recurring.Months.Select(m => m.Bills).ToList(), ChartStyle.Accent),
            ]);

        Subscriptions.Clear();
        Bills.Clear();
        foreach (var payment in recurring.Payments)
        {
            (payment.Kind == RecurringKinds.Bill ? Bills : Subscriptions).Add(RecurringItem.From(payment));
        }

        HasSubscriptions = Subscriptions.Count > 0;
        HasBills = Bills.Count > 0;
    }

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
