using System.ComponentModel;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Profile;

public partial class ProfilePage : ContentPage, ITabRoot
{
    private readonly ProfileViewModel _viewModel;

    public ProfilePage(ProfileViewModel viewModel)
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

    /// <summary>A second tap on the Profile tab: back to the top.</summary>
    public void ResetView() => _ = Scroller.ScrollToAsync(0, 0, animated: true);

    /// <summary>
    /// Keeps the keyboard in step with the in-place panels: it comes up with a field
    /// that just opened and goes away when that field's panel closes.
    /// </summary>
    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ProfileViewModel.Panel))
        {
            SyncKeyboard(JoinCodeEntry, _viewModel.IsJoinOpen);
        }
        else if (e.PropertyName == nameof(ProfileViewModel.ConnectStep))
        {
            SyncKeyboard(LunchFlowKeyEntry, _viewModel.IsLunchFlowOpen);
            SyncKeyboard(Trading212KeyEntry, _viewModel.IsTrading212Open);
            SyncKeyboard(FioTokenEntry, _viewModel.IsFioOpen);
        }
        else if (e.PropertyName == nameof(ProfileViewModel.IsConnecting) && _viewModel.IsConnecting)
        {
            // Out of the way, so the spinner and what is being waited for can be seen.
            SyncKeyboard(LunchFlowKeyEntry, false);
            SyncKeyboard(Trading212KeyEntry, false);
            SyncKeyboard(FioTokenEntry, false);
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
