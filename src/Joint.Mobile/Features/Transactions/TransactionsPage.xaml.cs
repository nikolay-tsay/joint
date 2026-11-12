namespace Joint.Mobile.Features.Transactions;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Load once; after that the user refreshes by pulling down, which keeps their scroll position.
        if (!_viewModel.HasLoaded)
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
