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

    /// <summary>
    /// Bound to the page's RefreshView. It is on only while the page's data is being
    /// reloaded, never for other actions: a RefreshView runs its refresh command whenever
    /// this turns on, so tying it to general busyness would reload on every button press.
    /// </summary>
    [ObservableProperty]
    public partial bool IsRefreshing { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>
    /// Reloads the page's data. A request that arrives while a reload is already running
    /// is dropped, which also absorbs the echo the RefreshView sends when the flag turns on.
    /// </summary>
    protected async Task ReloadAsync(Func<Task> load)
    {
        if (_isReloading)
        {
            return;
        }

        _isReloading = true;
        IsRefreshing = true;
        try
        {
            await RunAsync(load);
        }
        finally
        {
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
