namespace Joint.Mobile.Features.Categories;

public partial class CategoryPickerPage : ContentPage
{
    private readonly CategoryPickerViewModel _viewModel;

    public CategoryPickerPage(CategoryPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
