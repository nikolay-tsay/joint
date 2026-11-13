namespace Joint.Mobile.Features.Budget;

public partial class BudgetPage : ContentPage
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
}
