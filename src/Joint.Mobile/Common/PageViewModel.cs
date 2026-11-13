using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Mobile.Api;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Common;

/// <summary>
/// Base for page view models: a refresh flag, an error line, and one place that turns
/// a lost session or a network failure into the right reaction.
/// </summary>
public abstract partial class PageViewModel(AuthService auth) : ObservableObject
{
    private bool _isReloading;
    private bool _hasLoaded;

    /// <summary>
    /// Bound to the page's RefreshView, which shows its spinner while this is on. It is on
    /// for the page's first load and while the user pulls to refresh, never for other
    /// actions: a RefreshView runs its refresh command whenever this turns on, so tying it
    /// to general busyness would reload on every button press.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>
    /// Reloads the page's data. A request that arrives while a reload is already running
    /// is dropped, which also absorbs the echo the RefreshView sends when the flag turns on.
    /// Pages reload every time they are shown; after the first time that happens quietly,
    /// with what is on screen staying put until the new data replaces it. A pull shows the
    /// spinner because the RefreshView itself turns the flag on before calling in.
    /// </summary>
    protected async Task ReloadAsync(Func<Task> load)
    {
        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        if (!_hasLoaded)
        {
            IsRefreshing = true;
        }

        try
        {
            await RunAsync(load);
        }
        finally
        {
            _hasLoaded = true;
            IsRefreshing = false;
            _isReloading = false;
        }
    }

    /// <summary>Runs a server action with the shared error handling.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        Error = null;
        try
        {
            await action();
        }
        catch (SignedOutException)
        {
            await SignOutAsync();
        }
        catch (HttpRequestException)
        {
            Error = "Couldn't reach the server. Pull down to try again.";
        }
    }

    protected async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        await Shell.Current.GoToAsync(Routes.Login);
    }
}
