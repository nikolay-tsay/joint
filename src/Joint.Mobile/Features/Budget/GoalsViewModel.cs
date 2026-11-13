using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Budget;

/// <summary>
/// What the household is saving and investing towards, as two lists. A goal's progress
/// follows the balance of an account set aside for it, or is a figure kept by hand. Goals
/// of both kinds are added and changed in one form that opens in place.
/// </summary>
public partial class GoalsViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    private const string DeleteFirstTap = "Delete this goal";
    private const string DeleteSecondTap = "Tap again to delete it";

    private List<AccountResponse> _accounts = [];

    /// <summary>The goal the form is changing; null while it is adding a new one.</summary>
    private Guid? _editingId;

    /// <summary>The account picked in the form; null for "by hand".</summary>
    private Guid? _sourceAccountId;

    public ObservableCollection<GoalItem> SavingGoals { get; } = [];

    public ObservableCollection<GoalItem> InvestmentGoals { get; } = [];

    [ObservableProperty]
    public partial bool HasSavingGoals { get; set; }

    [ObservableProperty]
    public partial bool HasInvestmentGoals { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyVisible))]
    public partial bool HasNoGoals { get; set; }

    // ----- The form -----

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmptyVisible), nameof(IsAddVisible))]
    public partial bool IsFormOpen { get; set; }

    public bool IsEmptyVisible => HasNoGoals && !IsFormOpen;

    public bool IsAddVisible => !IsFormOpen;

    /// <summary>"New goal" or "Change goal".</summary>
    [ObservableProperty]
    public partial string? FormTitle { get; set; }

    /// <summary>Whether the form's goal is an investment goal; a saving goal otherwise.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSavingKind), nameof(SavedPlaceholder), nameof(NamePlaceholder))]
    public partial bool IsInvestmentKind { get; set; }

    public bool IsSavingKind => !IsInvestmentKind;

    public string NamePlaceholder => IsInvestmentKind ? "What you are investing towards" : "What you are saving for";

    public string SavedPlaceholder => IsInvestmentKind ? "Invested so far" : "Saved so far";

    [ObservableProperty]
    public partial string? NameInput { get; set; }

    [ObservableProperty]
    public partial string? TargetInput { get; set; }

    /// <summary>What is put aside so far, for a goal kept by hand.</summary>
    [ObservableProperty]
    public partial string? SavedInput { get; set; }

    [ObservableProperty]
    public partial string? FormError { get; set; }

    /// <summary>"By hand" and the household's accounts that suit the goal's kind.</summary>
    public ObservableCollection<GoalSourceItem> Sources { get; } = [];

    /// <summary>True when the form's goal has no account to follow, so the amount is typed.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFollowing))]
    public partial bool IsKeptByHand { get; set; } = true;

    public bool IsFollowing => !IsKeptByHand;

    /// <summary>True when the form is changing a goal, which can then also be deleted.</summary>
    [ObservableProperty]
    public partial bool CanDelete { get; set; }

    /// <summary>Deleting takes two taps; the button says which one is next.</summary>
    [ObservableProperty]
    public partial string DeleteLabel { get; set; } = DeleteFirstTap;

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () =>
    {
        // Fetch first, then swap: clearing before the await lets two loads interleave and double up.
        var goals = await api.GetGoalsAsync();
        _accounts = await api.GetAccountsAsync();
        Show(goals);
    });

    [RelayCommand]
    private void StartAdd() => OpenForm(null);

    [RelayCommand]
    private void StartEdit(GoalItem goal) => OpenForm(goal);

    [RelayCommand]
    private void CancelForm() => IsFormOpen = false;

    [RelayCommand]
    private void PickSaving() => SetKind(isInvestment: false);

    [RelayCommand]
    private void PickInvestment() => SetKind(isInvestment: true);

    /// <summary>Picks where the goal's progress comes from.</summary>
    [RelayCommand]
    private void SelectSource(GoalSourceItem source)
    {
        _sourceAccountId = source.AccountId;
        RebuildSources();
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(NameInput))
        {
            FormError = "Give the goal a name.";
            return;
        }

        if (!Money.TryRead(TargetInput, out var target) || target <= 0)
        {
            FormError = "Enter the target as an amount above zero.";
            return;
        }

        var saved = 0m;
        if (IsKeptByHand && !string.IsNullOrWhiteSpace(SavedInput) && (!Money.TryRead(SavedInput, out saved) || saved < 0))
        {
            FormError = "Enter the amount so far as a number, or leave it empty.";
            return;
        }

        var request = new SaveGoalRequest(
            NameInput.Trim(), target, _sourceAccountId, saved, IsInvestmentKind ? GoalKinds.Investment : GoalKinds.Saving);
        await RunAsync(async () =>
        {
            if (await api.SaveGoalAsync(_editingId, request))
            {
                IsFormOpen = false;
                Show(await api.GetGoalsAsync());
            }
            else
            {
                FormError = "That was not accepted. Check the name and the amounts.";
            }
        });
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        if (_editingId is not { } goalId)
        {
            return;
        }

        if (DeleteLabel != DeleteSecondTap)
        {
            DeleteLabel = DeleteSecondTap;
            return;
        }

        await RunAsync(async () =>
        {
            await api.DeleteGoalAsync(goalId);
            IsFormOpen = false;
            Show(await api.GetGoalsAsync());
        });
    }

    private void OpenForm(GoalItem? goal)
    {
        _editingId = goal?.Id;
        _sourceAccountId = goal?.AccountId;
        IsInvestmentKind = goal?.Kind == GoalKinds.Investment;
        FormTitle = goal is null ? "New goal" : "Change goal";
        NameInput = goal?.Name;
        TargetInput = goal is null ? null : Money.ForInput(goal.TargetAmount);
        SavedInput = goal is { AccountId: null } ? Money.ForInput(goal.Saved) : null;
        FormError = null;
        CanDelete = goal is not null;
        DeleteLabel = DeleteFirstTap;
        RebuildSources();
        IsFormOpen = true;
    }

    private void SetKind(bool isInvestment)
    {
        IsInvestmentKind = isInvestment;
        RebuildSources();
    }

    /// <summary>
    /// An investment goal can follow an investment account, a saving goal any other. An
    /// account already followed stays offered even if its type has since been changed.
    /// </summary>
    private void RebuildSources()
    {
        var fitting = _accounts
            .Where(a => (a.Kind == AccountKinds.Investment) == IsInvestmentKind || a.Id == _sourceAccountId)
            .ToList();
        if (_sourceAccountId is { } picked && fitting.All(a => a.Id != picked))
        {
            _sourceAccountId = null;
        }

        Sources.Clear();
        Sources.Add(new GoalSourceItem(null, "By hand", _sourceAccountId is null));
        foreach (var account in fitting)
        {
            Sources.Add(new GoalSourceItem(account.Id, account.Name, account.Id == _sourceAccountId));
        }

        IsKeptByHand = _sourceAccountId is null;
    }

    private void Show(List<GoalResponse> goals)
    {
        SavingGoals.Clear();
        InvestmentGoals.Clear();
        foreach (var goal in goals)
        {
            (goal.Kind == GoalKinds.Investment ? InvestmentGoals : SavingGoals).Add(GoalItem.From(goal));
        }

        HasSavingGoals = SavingGoals.Count > 0;
        HasInvestmentGoals = InvestmentGoals.Count > 0;
        HasNoGoals = goals.Count == 0;
    }
}
