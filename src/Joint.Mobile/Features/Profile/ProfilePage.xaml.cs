using System.ComponentModel;

namespace Joint.Mobile.Features.Profile;

public partial class ProfilePage : ContentPage
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
