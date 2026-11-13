using System.ComponentModel;

namespace Joint.Mobile.Features.Budget;

public partial class BudgetPage : ContentPage
{
    private readonly BudgetViewModel _viewModel;

    public BudgetPage(BudgetViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>The keyboard comes up with the budget field and goes away when its panel closes.</summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(BudgetViewModel.IsEditing))
        {
            return;
        }

        var isOpen = _viewModel.IsEditing;

        // After the layout pass that shows or hides the panel, so the field can take focus.
        Dispatcher.Dispatch(async () =>
        {
            if (isOpen)
            {
                LimitEntry.Focus();
            }
            else
            {
                LimitEntry.Unfocus();
                if (LimitEntry.IsSoftInputShowing())
                {
                    await LimitEntry.HideSoftInputAsync(CancellationToken.None);
                }
            }
        });
    }
}
