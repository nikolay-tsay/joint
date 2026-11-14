using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Home;

public partial class HomePage : ContentPage, ITabRoot
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }

    /// <summary>A second tap on the Home tab: back to the top.</summary>
    public void ResetView() => _ = Scroller.ScrollToAsync(0, 0, animated: true);
}
