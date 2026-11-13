using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Joint.Contracts;
using Joint.Mobile.Api;
using Joint.Mobile.Common;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Features.Budget;

/// <summary>
/// What the household is saving and investing towards, as two lists. Progress is not
/// entered anywhere: a saving goal is measured against the combined balance of the accounts
/// of the Savings type, an investment goal against those of the Investments type. Goals of
/// both kinds are added and changed in one form that opens in place.
/// </summary>
public partial class GoalsViewModel(ApiClient api, AuthService auth) : PageViewModel(auth)
{
    private const string DeleteFirstTap = "Delete this goal";
    private const string DeleteSecondTap = "Tap again to delete it";

    /// <summary>The goal the form is changing; null while it is adding a new one.</summary>
    private Guid? _editingId;

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
    [NotifyPropertyChangedFor(nameof(IsSavingKind), nameof(NamePlaceholder), nameof(ProgressNote))]
    public partial bool IsInvestmentKind { get; set; }

    public bool IsSavingKind => !IsInvestmentKind;

    public string NamePlaceholder => IsInvestmentKind ? "What you are investing towards" : "What you are saving for";

    /// <summary>How the goal's progress is worked out, since there is nothing to choose.</summary>
    public string ProgressNote => IsInvestmentKind
        ? "Progress is the combined balance of every account set to the Investments type. "
            + "You set an account's type by opening it from Home. Investment goals all share that one figure."
        : "Progress is the combined balance of every account set to the Savings type. "
            + "You set an account's type by opening it from Home. Saving goals all share that one figure.";

    [ObservableProperty]
    public partial string? NameInput { get; set; }

    [ObservableProperty]
    public partial string? TargetInput { get; set; }

    [ObservableProperty]
    public partial string? FormError { get; set; }

    /// <summary>True when the form is changing a goal, which can then also be deleted.</summary>
    [ObservableProperty]
    public partial bool CanDelete { get; set; }

    /// <summary>Deleting takes two taps; the button says which one is next.</summary>
    [ObservableProperty]
    public partial string DeleteLabel { get; set; } = DeleteFirstTap;

    [RelayCommand]
    private Task LoadAsync() => ReloadAsync(async () => Show(await api.GetGoalsAsync()));

    [RelayCommand]
    private void StartAdd() => OpenForm(null);

    [RelayCommand]
    private void StartEdit(GoalItem goal) => OpenForm(goal);

    [RelayCommand]
    private void CancelForm() => IsFormOpen = false;

    [RelayCommand]
    private void PickSaving() => IsInvestmentKind = false;

    [RelayCommand]
    private void PickInvestment() => IsInvestmentKind = true;

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

        var request = new SaveGoalRequest(
            NameInput.Trim(), target, IsInvestmentKind ? GoalKinds.Investment : GoalKinds.Saving);
        await RunAsync(async () =>
        {
            if (await api.SaveGoalAsync(_editingId, request))
            {
                IsFormOpen = false;
                Show(await api.GetGoalsAsync());
            }
            else
            {
                FormError = "That was not accepted. Check the name and the amount.";
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
        IsInvestmentKind = goal?.Kind == GoalKinds.Investment;
        FormTitle = goal is null ? "New goal" : "Change goal";
        NameInput = goal?.Name;
        TargetInput = goal is null ? null : Money.ForInput(goal.TargetAmount);
        FormError = null;
        CanDelete = goal is not null;
        DeleteLabel = DeleteFirstTap;
        IsFormOpen = true;
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
