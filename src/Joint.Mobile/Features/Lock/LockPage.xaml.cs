namespace Joint.Mobile.Features.Lock;

public partial class LockPage : ContentPage
{
    private readonly LockViewModel _viewModel;
    private bool _started;

    public LockPage(LockViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>Completes with true once unlocked, or false once signed out.</summary>
    public Task<bool> WaitAsync() => _viewModel.Done;

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Appearing fires again when the biometric prompt closes; ask only once by ourselves.
        if (!_started)
        {
            _started = true;
            await _viewModel.StartAsync();
        }
    }

    /// <summary>Back must not dismiss the lock.</summary>
    protected override bool OnBackButtonPressed() => true;
}
