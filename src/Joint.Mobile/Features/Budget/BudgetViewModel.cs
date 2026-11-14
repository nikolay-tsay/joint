using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Budget;

/// <summary>
/// The household's one monthly budget for everyday spending: what is left and where the
/// month is heading, and the parts of it set aside for single categories. Subscriptions and
/// regular payments are outside it and only shown for context. Everything is set and changed
/// in place on the page.
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
    [NotifyPropertyChangedFor(nameof(IsSummaryVisible), nameof(IsPromptVisible), nameof(IsCreating))]
    public partial bool HasBudget { get; set; }

    /// <summary>True while the page shows the field for typing the budget.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSummaryVisible), nameof(IsPromptVisible), nameof(IsCreating))]
    public partial bool IsEditing { get; set; }

    public bool IsSummaryVisible => HasBudget && !IsEditing;

    public bool IsPromptVisible => !HasBudget && !IsEditing;

    /// <summary>Making a new budget: its categories' parts can be added in the same card.</summary>
    public bool IsCreating => !HasBudget && IsEditing;

    /// <summary>The categories' parts lined up for a budget not yet saved.</summary>
    public ObservableCollection<CategoryDraft> Drafts { get; } = [];

    [ObservableProperty]
    public partial bool HasDrafts { get; set; }

    private IReadOnlyList<string> _categoryNames = [];

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

    /// <summary>
    /// Under the budget field when categories have parts: what lowering or removing the
    /// budget does to them.
    /// </summary>
    [ObservableProperty]
    public partial string? LinesNote { get; set; }

    // ----- Parts of the budget for single categories -----

    /// <summary>What the categories add up to; the budget cannot go below it without shrinking them.</summary>
    private decimal _assigned;

    private decimal _limit;

    /// <summary>The categories with a part of the budget, then "Everything else".</summary>
    public ObservableCollection<CategoryLineItem> Lines { get; } = [];

    [ObservableProperty]
    public partial bool HasLines { get; set; }

    /// <summary>"3 000,00 CZK of the budget is not given to a category."</summary>
    [ObservableProperty]
    public partial string? Unassigned { get; set; }

    /// <summary>False once every category has a part or nothing of the budget is left to give.</summary>
    [ObservableProperty]
    public partial bool CanAddLine { get; set; }

    [ObservableProperty]
    public partial bool IsAddingLine { get; set; }

    /// <summary>The categories not yet in the budget, to pick one from.</summary>
    public ObservableCollection<CategoryChip> LineChoices { get; } = [];

    [ObservableProperty]
    public partial string? NewLineAmount { get; set; }

    [ObservableProperty]
    public partial string? NewLineError { get; set; }

    /// <summary>"Up to 3 000,00 CZK".</summary>
    [ObservableProperty]
    public partial string? NewLineHint { get; set; }

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () => Show(await api.GetBudgetAsync()));

    [RelayCommand]
    private Task StartAddLineAsync() => RunAsync(async () =>
    {
        var names = (await api.GetCategoriesAsync(null)).Names;
        var taken = Lines.Where(l => !l.IsRest).Select(l => l.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        LineChoices.Clear();
        foreach (var name in names.Where(n => !taken.Contains(n)))
        {
            LineChoices.Add(new CategoryChip(name));
        }

        CloseLines();
        NewLineAmount = null;
        NewLineError = LineChoices.Count == 0 ? "Every category already has a part of the budget." : null;
        NewLineHint = $"Up to {Format(_limit - _assigned)}";
        IsAddingLine = true;
    });

    [RelayCommand]
    private void CancelAddLine() => IsAddingLine = false;

    [RelayCommand]
    private void PickLineCategory(CategoryChip chip)
    {
        // Tapping the picked one again unticks it.
        var pick = !chip.IsSelected;
        foreach (var choice in LineChoices)
        {
            choice.IsSelected = pick && choice == chip;
        }

        NewLineError = null;
    }

    [RelayCommand]
    private Task SaveNewLineAsync()
    {
        if (LineChoices.FirstOrDefault(c => c.IsSelected) is not { } chip)
        {
            NewLineError = "Pick a category.";
            return Task.CompletedTask;
        }

        return StoreLineAsync(chip.Name, NewLineAmount, room: _limit - _assigned, error => NewLineError = error);
    }

    /// <summary>Opens a category's row to change or remove its part; one row is open at a time.</summary>
    [RelayCommand]
    private void ToggleLine(CategoryLineItem line)
    {
        if (line.IsRest)
        {
            return;
        }

        var open = !line.IsOpen;
        CloseLines();
        IsAddingLine = false;
        line.Error = null;
        line.AmountInput = Money.ForInput(line.Limit);
        line.IsOpen = open;
    }

    [RelayCommand]
    private Task SaveLineAsync(CategoryLineItem line) =>
        StoreLineAsync(line.Name, line.AmountInput, room: _limit - _assigned + line.Limit, error => line.Error = error);

    [RelayCommand]
    private Task RemoveLineAsync(CategoryLineItem line) => RunAsync(async () =>
    {
        await api.RemoveCategoryBudgetAsync(line.Name);
        Show(await api.GetBudgetAsync());
    });

    /// <summary>Checks the amount fits in what the budget has left before sending it, so the answer is immediate.</summary>
    private Task StoreLineAsync(string category, string? input, decimal room, Action<string> showError)
    {
        if (!Money.TryRead(input, out var amount) || amount <= 0)
        {
            showError("Enter an amount above zero.");
            return Task.CompletedTask;
        }

        if (amount > room)
        {
            showError($"Only {Format(room)} of the budget is left for this category.");
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            if (!await api.SetCategoryBudgetAsync(category, amount))
            {
                showError("That amount was not accepted.");
                return;
            }

            IsAddingLine = false;
            Show(await api.GetBudgetAsync());
        });
    }

    private void CloseLines()
    {
        foreach (var line in Lines)
        {
            line.IsOpen = false;
        }
    }

    [RelayCommand]
    private Task StartEditAsync()
    {
        LimitError = null;
        IsAddingLine = false;
        IsEditing = true;
        if (HasBudget)
        {
            return Task.CompletedTask;
        }

        // A new budget can be given its categories' parts before it is saved.
        Drafts.Clear();
        NewLineAmount = null;
        NewLineError = null;
        NewLineHint = "Amount for the category";
        return RunAsync(async () =>
        {
            _categoryNames = (await api.GetCategoriesAsync(null)).Names;
            RefreshDraftChoices();
        });
    }

    [RelayCommand]
    private void CancelEdit() => IsEditing = false;

    /// <summary>Puts a category's part on the new budget's list; nothing is saved until the budget is.</summary>
    [RelayCommand]
    private void AddDraft()
    {
        if (LineChoices.FirstOrDefault(c => c.IsSelected) is not { } chip)
        {
            NewLineError = "Pick a category.";
            return;
        }

        if (!Money.TryRead(NewLineAmount, out var amount) || amount <= 0)
        {
            NewLineError = "Enter an amount above zero.";
            return;
        }

        if (Money.TryRead(LimitInput, out var limit) && Drafts.Sum(d => d.Amount) + amount > limit)
        {
            NewLineError = $"Only {Format(Math.Max(limit - Drafts.Sum(d => d.Amount), 0))} of the budget is left for categories.";
            return;
        }

        Drafts.Add(new CategoryDraft(chip.Name, amount, Format(amount)));
        NewLineAmount = null;
        NewLineError = null;
        RefreshDraftChoices();
    }

    [RelayCommand]
    private void RemoveDraft(CategoryDraft draft)
    {
        Drafts.Remove(draft);
        RefreshDraftChoices();
    }

    /// <summary>The categories still without a part on the new budget's list.</summary>
    private void RefreshDraftChoices()
    {
        LineChoices.Clear();
        foreach (var name in _categoryNames.Where(n => Drafts.All(d => !string.Equals(d.Name, n, StringComparison.OrdinalIgnoreCase))))
        {
            LineChoices.Add(new CategoryChip(name));
        }

        HasDrafts = Drafts.Count > 0;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!Money.TryRead(LimitInput, out var limit) || limit <= 0)
        {
            LimitError = "Enter an amount above zero.";
            return;
        }

        if (HasBudget)
        {
            await StoreAsync(limit);
            return;
        }

        // A category picked with its amount typed counts even if "Add category" was not tapped:
        // dropping it without a word left people with a budget missing the part they had just set.
        var picked = LineChoices.FirstOrDefault(c => c.IsSelected);
        var typed = !string.IsNullOrWhiteSpace(NewLineAmount);
        if (picked is not null || typed)
        {
            if (picked is null)
            {
                NewLineError = "Pick the category for that amount, or clear it.";
                return;
            }

            if (!typed)
            {
                NewLineError = $"Enter an amount for {picked.Name}, or tap it again to untick it.";
                return;
            }

            var before = Drafts.Count;
            AddDraft();
            if (Drafts.Count == before)
            {
                return;
            }
        }

        var parts = Drafts.Sum(d => d.Amount);
        if (parts > limit)
        {
            LimitError = $"The categories add up to {Format(parts)}, more than the budget.";
            return;
        }

        await StoreAsync(limit, Drafts.Select(d => new SetCategoryBudgetRequest(d.Name, d.Amount)).ToList());
    }

    [RelayCommand]
    private Task RemoveAsync() => StoreAsync(null);

    private Task StoreAsync(decimal? limit, List<SetCategoryBudgetRequest>? categories = null) => RunAsync(async () =>
    {
        if (await api.SetBudgetAsync(limit, categories))
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
        ShowLines(budget);
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

    /// <summary>
    /// The categories' parts and "Everything else": the budget less those parts, against
    /// everyday spending less what went to those categories.
    /// </summary>
    private void ShowLines(BudgetResponse budget)
    {
        var categories = budget.Categories ?? [];
        _limit = budget.Limit ?? 0;
        _assigned = categories.Sum(c => c.Limit);

        Lines.Clear();
        HasLines = budget.Limit is not null && categories.Count > 0;
        if (budget.Limit is null)
        {
            IsAddingLine = false;
            CanAddLine = false;
            LinesNote = null;
            return;
        }

        foreach (var category in categories)
        {
            Lines.Add(new CategoryLineItem(category.Name, category.Limit, category.Spent, _currency));
        }

        if (HasLines)
        {
            Lines.Add(new CategoryLineItem(
                CategoryLineItem.RestName,
                _limit - _assigned,
                Math.Max(budget.Spent - categories.Sum(c => c.Spent), 0),
                _currency,
                isRest: true));
        }

        CanAddLine = _limit - _assigned > 0;
        Unassigned = HasLines
            ? $"{Format(_assigned)} is given to categories, {Format(_limit - _assigned)} is left for everything else."
            : "Set aside part of the budget for some categories, like groceries or eating out. "
                + "Whatever you do not give to a category stays as \"Everything else\".";
        LinesNote = HasLines
            ? $"Categories have {Format(_assigned)} of it. A lower budget shrinks each category's part "
                + "in proportion; removing the budget removes them too."
            : null;
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
