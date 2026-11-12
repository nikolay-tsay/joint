namespace Joint.Mobile.Features.Transactions;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;
    private bool _hasTypeButton;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Only a single account can be given a type; the household-wide tab has no such button.
        if (_viewModel.IsAccountView && !_hasTypeButton)
        {
            ToolbarItems.Add(new ToolbarItem { Text = "Type", Command = _viewModel.ChangeTypeCommand });
            _hasTypeButton = true;
        }

        // Load once; after that the user refreshes by pulling down, which keeps their scroll position.
        if (!_viewModel.HasLoaded)
        {
            _viewModel.LoadCommand.Execute(null);
        }
    }
}
