using System.ComponentModel;

namespace Joint.Mobile.Features.Lock;

public partial class LockSettingsPage : ContentPage
{
    private readonly LockSettingsViewModel _viewModel;

    public LockSettingsPage(LockSettingsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    /// <summary>
    /// Keeps the keyboard in step with the in-place panels: it comes up with a field
    /// that just opened and goes away when that field's panel closes.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LockSettingsViewModel.IsPinSetupOpen))
        {
            // Changing a PIN starts at the current one; choosing the first PIN has no such field.
            SyncKeyboard(_viewModel.IsLockOn ? CurrentPinEntry : NewPinEntry, _viewModel.IsPinSetupOpen);
        }
        else if (e.PropertyName == nameof(LockSettingsViewModel.IsTurnOffOpen))
        {
            SyncKeyboard(TurnOffPinEntry, _viewModel.IsTurnOffOpen);
        }
    }

    private void SyncKeyboard(Entry entry, bool isOpen) =>
        // After the layout pass that shows or hides the panel, so the field can take focus.
        Dispatcher.Dispatch(async () =>
        {
            if (isOpen)
            {
                entry.Focus();
            }
            else
            {
                entry.Unfocus();
                if (entry.IsSoftInputShowing())
                {
                    await entry.HideSoftInputAsync(CancellationToken.None);
                }
            }
        });
}
