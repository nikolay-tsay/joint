using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Transactions;

public partial class TransactionsPage : ContentPage, ITabRoot
{
    private readonly TransactionsViewModel _viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Load once; after that the user refreshes by pulling down, which keeps their scroll
        // position. A filter sent from another page (the category chart) is the exception,
        // and its list starts at the top.
        if (!_viewModel.HasLoaded)
        {
            _viewModel.LoadCommand.Execute(null);
        }
        else if (_viewModel.NeedsReload)
        {
            await _viewModel.LoadCommand.ExecuteAsync(null);
            ScrollToTop();
        }
        else
        {
            // Back from the category page or another tab: the labels may have changed there.
            _viewModel.RefreshMarksCommand.Execute(null);
        }
    }

    /// <summary>A second tap on the Transactions tab: everything put away and back to the newest.</summary>
    public void ResetView()
    {
        _viewModel.CloseOpenParts();
        ScrollToTop();
    }

    private void ScrollToTop()
    {
        if (_viewModel.Groups.Count > 0 && _viewModel.Groups[0].Count > 0)
        {
            TransactionList.ScrollTo(0, groupIndex: 0, position: ScrollToPosition.Start, animate: true);
        }
    }
}
