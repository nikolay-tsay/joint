using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Budget;

/// <summary>
/// The household's one monthly budget for everyday spending: what is left and where the
/// month is heading. Subscriptions and regular payments are outside it and only shown for
/// context. The budget is set and changed in place on the page.
/// </summary>
public partial class BudgetViewModel(ApiClient api, AuthService auth, GoalsViewModel goals) : PageViewModel(auth)
{
    private string _currency = "";
    private IReadOnlyList<BudgetView>? _views;

    /// <summary>The saving goals, shown on the page's second tab.</summary>
    public GoalsViewModel Goals { get; } = goals;

    /// <summary>The pages the carousel slides between, in tab order.</summary>
    public IReadOnlyList<BudgetView> Views =>
        _views ??= [new BudgetView(BudgetView.Budget, this), new BudgetView(BudgetView.Goals, this)];

    /// <summary>
    /// Which page is showing. The carousel sets it when the user drags, and slides to it
    /// when a tab sets it.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBudgetTab), nameof(IsGoalsTab))]
    public partial int TabIndex { get; set; }

    public bool IsBudgetTab => TabIndex == 0;

    public bool IsGoalsTab => TabIndex == 1;

    [RelayCommand]
    private void ShowBudget() => TabIndex = 0;

    [RelayCommand]
    private void ShowGoals() => TabIndex = 1;

    /// <summary>"November budget".</summary>
    [ObservableProperty]
    public partial string? Title { get; set; }

    /// <summary>False until a connected account has transactions; there is nothing to budget before.</summary>
    [ObservableProperty]
    public partial bool HasData { get; set; }

    [ObservableProperty]
    public partial bool HasNoData { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummaryVisible), nameof(IsPromptVisible))]
    public partial bool HasBudget { get; set; }

    /// <summary>True while the page shows the field for typing the budget.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummaryVisible), nameof(IsPromptVisible))]
    public partial bool IsEditing { get; set; }

    public bool IsSummaryVisible => HasBudget && !IsEditing;

    public bool IsPromptVisible => !HasBudget && !IsEditing;

    // ----- With a budget -----

    /// <summary>"Left to spend", or "Over budget" once the limit is passed.</summary>
    [ObservableProperty]
    public partial string? LeftTitle { get; set; }

    [ObservableProperty]
    public partial string? LeftAmount { get; set; }

    /// <summary>"Spent 27 600,00 of 40 000,00 CZK".</summary>
    [ObservableProperty]
    public partial string? SpentOf { get; set; }

    /// <summary>Spent against the limit, 0 to 1, for the bar.</summary>
    [ObservableProperty]
    public partial double Progress { get; set; }

    /// <summary>What can be spent per remaining day; empty when nothing is left.</summary>
    [ObservableProperty]
    public partial string? DailyAllowance { get; set; }

    [ObservableProperty]
    public partial string? Projection { get; set; }

    /// <summary>The projection against the limit, in words.</summary>
    [ObservableProperty]
    public partial string? Verdict { get; set; }

    [ObservableProperty]
    public partial bool IsProjectedOver { get; set; }

    // ----- The month's figures, shown with or without a budget -----

    [ObservableProperty]
    public partial string? Spent { get; set; }

    [ObservableProperty]
    public partial string? FixedPaid { get; set; }

    [ObservableProperty]
    public partial string? FixedExpected { get; set; }

    /// <summary>Everyday spending and fixed costs together.</summary>
    [ObservableProperty]
    public partial string? Total { get; set; }

    [ObservableProperty]
    public partial string? LastMonth { get; set; }

    // ----- Setting the budget -----

    /// <summary>What the user is typing as the monthly limit.</summary>
    [ObservableProperty]
    public partial string? LimitInput { get; set; }

    [ObservableProperty]
    public partial string? LimitError { get; set; }

    /// <summary>"Budget per month, in CZK".</summary>
    [ObservableProperty]
    public partial string? LimitPlaceholder { get; set; }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () => Show(await api.GetBudgetAsync()));

    [RelayCommand]
    private void StartEdit()
    {
        LimitError = null;
        IsEditing = true;
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Money.TryRead(LimitInput, out var limit) || limit <= 0)
        {
            LimitError = "Enter an amount above zero.";
            return;
        }

        await StoreAsync(limit);
    }

    [RelayCommand]
    private Task RemoveAsync() => StoreAsync(null);

    private Task StoreAsync(decimal? limit) => RunAsync(async () =>
    {
        if (await api.SetBudgetAsync(limit))
        {
            IsEditing = false;
            Show(await api.GetBudgetAsync());
        }
        else
        {
            LimitError = "That amount was not accepted.";
        }
    });

    private void Show(BudgetResponse budget)
    {
        HasData = budget.Currency is not null;
        HasNoData = !HasData;
        _currency = budget.Currency ?? "";

        Title = $"{budget.Month:MMMM} budget";
        Spent = Format(budget.Spent);
        FixedPaid = Format(budget.FixedPaid);
        FixedExpected = Format(budget.FixedExpected);
        Total = Format(budget.Spent + budget.FixedPaid);
        LastMonth = Format(budget.LastMonthSpent);
        Projection = $"At this pace everyday spending reaches about {Format(budget.Projected)} by the end of {budget.Month:MMMM}.";
        LimitPlaceholder = $"Budget per month, in {_currency}";

        HasBudget = budget.Limit is not null;
        if (budget.Limit is not { } limit)
        {
            LimitInput = null;
            return;
        }

        LimitInput = Money.ForInput(limit);

        var left = limit - budget.Spent;
        LeftTitle = left < 0 ? "Over budget" : "Left to spend";
        LeftAmount = Format(Math.Abs(left));
        SpentOf = $"Spent {Format(budget.Spent)} of {Format(limit)}";
        Progress = (double)Math.Clamp(budget.Spent / limit, 0, 1);
        DailyAllowance = DescribeAllowance(budget, limit);

        var projectedLeft = limit - budget.Projected;
        IsProjectedOver = projectedLeft < 0;
        Verdict = IsProjectedOver
            ? $"That is {Format(-projectedLeft)} over budget."
            : $"That is {Format(projectedLeft)} under budget.";
    }

    /// <summary>What is left of the budget, spread over the days that remain, today included.</summary>
    private string DescribeAllowance(BudgetResponse budget, decimal limit)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var daysInMonth = DateTime.DaysInMonth(budget.Month.Year, budget.Month.Month);
        var daysLeft = today.Year == budget.Month.Year && today.Month == budget.Month.Month
            ? daysInMonth - today.Day + 1
            : 0;
        var free = limit - budget.Spent;
        if (daysLeft <= 0 || free <= 0)
        {
            return "";
        }

        return daysLeft == 1
            ? $"About {Format(free)} for today."
            : $"About {Format(free / daysLeft)} a day for the remaining {daysLeft} days.";
    }

    private string Format(decimal amount) => Money.Format(amount, _currency);
}
