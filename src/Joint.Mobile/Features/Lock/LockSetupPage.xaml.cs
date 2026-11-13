namespace Joint.Mobile.Features.Lock;

public partial class LockSetupPage : ContentPage
{
    private readonly LockSetupViewModel _viewModel;

    public LockSetupPage(LockSetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Completes when the user has turned the lock on or declined.</summary>
    public Task WaitAsync() => _viewModel.Done;

    /// <summary>Back means "not now", the same as the button.</summary>
    protected override bool OnBackButtonPressed()
    {
        _viewModel.SkipCommand.Execute(null);
        return true;
    }
}
