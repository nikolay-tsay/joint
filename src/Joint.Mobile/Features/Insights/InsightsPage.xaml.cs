using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Insights;

public partial class InsightsPage : ContentPage, ITabRoot
{
    private readonly InsightsViewModel _viewModel;

    public InsightsPage(InsightsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// A finger on a chart, or moving across it, picks the month or day underneath. The
    /// chart marks it and the view model puts that period's numbers under the chart.
    /// </summary>
    private void OnChartTouched(object? sender, TouchEventArgs e)
    {
        if (sender is GraphicsView { Drawable: Chart chart } view
            && e.Touches.Length > 0
            && chart.SelectAt(e.Touches[0]))
        {
            view.Invalidate();
        }
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Categories may load first; it must already know the period.
        _viewModel.Categories.Period = _viewModel.Period.Current;
        _viewModel.LoadCommand.Execute(null);
        _viewModel.Categories.LoadCommand.Execute(null);
    }

    /// <summary>A second tap on the Insights tab: back to its first view, the overview.</summary>
    public void ResetView() => _viewModel.TabIndex = 0;
}
