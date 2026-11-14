using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Three views behind one tab. Overview: the chosen period at a glance, income against spending
/// for it and the periods before, what was left over, its pace against the previous period's,
/// and who spent. Overview and Categories follow the period chosen in <see cref="Period"/>.
/// Recurring: what subscriptions and regular payments cost per month, and the list of them.
/// </summary>
public partial class InsightsViewModel(ApiClient api, AuthService auth, CategoriesViewModel categories)
    : PageViewModel(auth)
{
    private const int Months = 6;

    /// <summary>Shown under a chart until it is touched.</summary>
    private const string MonthHint = "Touch a month to see its exact amounts.";

    private const string DayHint = "Touch a day to see the totals up to it.";

    private PeriodFilter? _period;

    /// <summary>The dates Overview and Categories report on; changing them reloads both.</summary>
    public PeriodFilter Period => _period ??= new PeriodFilter(PeriodChangedAsync);

    /// <summary>"Spending this month", or "Spending this period" for any other kind of period.</summary>
    [ObservableProperty]
    public partial string PaceTitle { get; set; } = "Spending this month";

    [ObservableProperty]
    public partial string PaceThisLabel { get; set; } = "This month";

    [ObservableProperty]
    public partial string PaceLastLabel { get; set; } = "Last month";

    [ObservableProperty]
    public partial string LeftOverTitle { get; set; } = "Left over each month";

    [ObservableProperty]
    public partial string MembersTitle { get; set; } = "Who spent this month";

    /// <summary>"Saved in November", or "Overspent in November" when more went out than came in.</summary>
    [ObservableProperty]
    public partial string? SavedTitle { get; set; }

    [ObservableProperty]
    public partial string? Saved { get; set; }

    [ObservableProperty]
    public partial string? Income { get; set; }

    [ObservableProperty]
    public partial string? Spending { get; set; }

    /// <summary>The period's spending against the same day of the period before, in words.</summary>
    [ObservableProperty]
    public partial string? PaceSummary { get; set; }

    // The exact numbers of the month or day last touched on each chart.

    [ObservableProperty]
    public partial string FlowDetail { get; set; } = MonthHint;

    [ObservableProperty]
    public partial string SavedDetail { get; set; } = MonthHint;

    [ObservableProperty]
    public partial string PaceDetail { get; set; } = DayHint;

    [ObservableProperty]
    public partial string RecurringDetail { get; set; } = MonthHint;

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

    private IReadOnlyList<InsightsView>? _views;

    /// <summary>The pages the carousel slides between, in tab order.</summary>
    public IReadOnlyList<InsightsView> Views =>
        _views ??=
        [
            new InsightsView(InsightsView.Overview, this),
            new InsightsView(InsightsView.Recurring, this),
            new InsightsView(InsightsView.Categories, this),
        ];

    /// <summary>Spending by category, shown on the page's third tab; it loads and reports errors by itself.</summary>
    public CategoriesViewModel Categories { get; } = categories;

    /// <summary>
    /// Which page is showing. The carousel sets it when the user drags, and slides to it
    /// when a tab sets it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewTab), nameof(IsRecurringTab), nameof(IsCategoriesTab), nameof(ShowsPeriod))]
    public partial int TabIndex { get; set; }

    public bool IsOverviewTab => TabIndex == 0;

    public bool IsRecurringTab => TabIndex == 1;

    public bool IsCategoriesTab => TabIndex == 2;

    /// <summary>The period bar sits over Overview and Categories; Recurring stays by calendar month.</summary>
    public bool ShowsPeriod => !IsRecurringTab;

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
    private void ShowOverview() => TabIndex = 0;

    [RelayCommand]
    private void ShowRecurring() => TabIndex = 1;

    [RelayCommand]
    private void ShowCategories() => TabIndex = 2;

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
        Categories.Period = Period.Current;
        var insights = await api.GetInsightsAsync(Months, Period.Current);
        FillRecurring(await api.GetRecurringAsync(Months), insights.Currency);

        HasData = insights.Currency is not null;
        HasNoData = !HasData;
        if (insights.Currency is not { } currency)
        {
            return;
        }

        // Each bar's own dates; an older backend sends calendar months without an end.
        var periods = insights.Months
            .Select(m => new DatePeriod(m.Month, m.End ?? m.Month.AddMonths(1).AddDays(-1)))
            .ToList();
        var period = periods[^1];
        var noun = PeriodText.Noun(period);
        PaceTitle = $"Spending this {noun}";
        PaceThisLabel = $"This {noun}";
        PaceLastLabel = period.IsCalendarMonth ? "Last month" : "Period before";
        LeftOverTitle = $"Left over each {noun}";
        MembersTitle = $"Who spent this {noun}";

        var current = insights.Months[^1];
        var net = current.Income - current.Spending;
        SavedTitle = $"{(net < 0 ? "Overspent" : "Saved")} {PeriodText.After(period)}";
        Saved = Money.Format(Math.Abs(net), currency);
        Income = Money.Format(current.Income, currency);
        Spending = Money.Format(current.Spending, currency);
        PaceSummary = DescribePace(insights, noun);

        var labels = periods.Select(PeriodText.Short).ToList();
        // New charts start with nothing picked, so the lines under them go back to the hint.
        FlowDetail = MonthHint;
        SavedDetail = MonthHint;
        PaceDetail = DayHint;

        FlowChart = new BarChart(
            labels,
            [
                new ChartSeries(insights.Months.Select(m => m.Income).ToList(), ChartStyle.Income),
                new ChartSeries(insights.Months.Select(m => m.Spending).ToList(), ChartStyle.Spending),
            ])
        {
            Selected = index =>
            {
                var month = insights.Months[index];
                FlowDetail = $"{PeriodText.Describe(periods[index])}: income {Money.Format(month.Income, currency)}, "
                    + $"spending {Money.Format(month.Spending, currency)}";
            },
        };
        SavedChart = new BarChart(
            labels,
            [new ChartSeries(insights.Months.Select(m => m.Income - m.Spending).ToList(), ChartStyle.Income)],
            negativeColor: ChartStyle.Negative)
        {
            Selected = index =>
            {
                var month = insights.Months[index];
                var left = month.Income - month.Spending;
                var when = PeriodText.Describe(periods[index]);
                SavedDetail = left < 0
                    ? $"{when}: {Money.Format(-left, currency)} more spent than came in"
                    : $"{when}: {Money.Format(left, currency)} left over";
            },
        };
        PaceChart = new LineChart(
        [
            new ChartSeries(insights.SpendingLastMonth, ChartStyle.Muted),
            new ChartSeries(insights.SpendingThisMonth, ChartStyle.Spending),
        ])
        {
            Selected = index =>
            {
                // A day this period has not reached yet, or the one before never had, has no figure.
                string At(List<decimal> running) =>
                    index < running.Count ? Money.Format(running[index], currency) : "no figure yet";

                PaceDetail = $"By day {index + 1}: {At(insights.SpendingThisMonth)} this {noun}, "
                    + $"{At(insights.SpendingLastMonth)} {(period.IsCalendarMonth ? "last month" : "the period before")}";
            },
        };

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

        RecurringDetail = MonthHint;
        RecurringChart = new BarChart(
            recurring.Months.Select(m => m.Month.ToString("MMM")).ToList(),
            [
                new ChartSeries(recurring.Months.Select(m => m.Subscriptions).ToList(), ChartStyle.Spending),
                new ChartSeries(recurring.Months.Select(m => m.Bills).ToList(), ChartStyle.Accent),
            ])
        {
            Selected = index =>
            {
                var month = recurring.Months[index];
                RecurringDetail = $"{month.Month:MMMM yyyy}: subscriptions {Money.Format(month.Subscriptions, currency)}, "
                    + $"regular payments {Money.Format(month.Bills, currency)}";
            },
        };

        Subscriptions.Clear();
        Bills.Clear();
        foreach (var payment in recurring.Payments)
        {
            (payment.Kind == RecurringKinds.Bill ? Bills : Subscriptions).Add(RecurringItem.From(payment));
        }

        HasSubscriptions = Subscriptions.Count > 0;
        HasBills = Bills.Count > 0;
    }

    /// <param name="noun">"month" or "period".</param>
    private static string DescribePace(InsightsResponse insights, string noun)
    {
        if (insights.SpendingThisMonth.Count == 0 || insights.SpendingLastMonth.Count == 0)
        {
            return "";
        }

        // The same day of the period before, or its last day when this one is already longer.
        var day = Math.Min(insights.SpendingThisMonth.Count, insights.SpendingLastMonth.Count) - 1;
        var now = insights.SpendingThisMonth[^1];
        var then = insights.SpendingLastMonth[day];
        var before = noun == "month" ? "by this day last month" : "by this day of the period before";
        if (then == 0)
        {
            return now == 0 ? $"Nothing spent yet this {noun}." : $"Nothing had been spent {before}.";
        }

        var change = (now - then) / then;
        return Math.Abs(change) < 0.01m
            ? $"About the same as {before}."
            : $"{Math.Abs(change):P0} {(change < 0 ? "less" : "more")} than {before}.";
    }

    /// <summary>A new period: both views that follow it load again.</summary>
    private Task PeriodChangedAsync()
    {
        Categories.Period = Period.Current;
        return Task.WhenAll(LoadCommand.ExecuteAsync(null), Categories.LoadCommand.ExecuteAsync(null));
    }
}
