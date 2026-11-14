using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Budget;

public partial class BudgetPage : ContentPage, ITabRoot
{
    private readonly BudgetViewModel _viewModel;

    public BudgetPage(BudgetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
        _viewModel.Goals.LoadCommand.Execute(null);
    }

    /// <summary>A second tap on the Budget tab: back to its first view, the budget.</summary>
    public void ResetView() => _viewModel.TabIndex = 0;
}
