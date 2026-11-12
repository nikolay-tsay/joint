using CommunityToolkit.Mvvm.ComponentModel;
using Joint.Mobile.Api;
using Joint.Mobile.Features.Auth;

namespace Joint.Mobile.Common;

/// <summary>
/// Base for page view models: a busy flag, an error line, and one place that turns
/// a lost session or a network failure into the right reaction.
/// </summary>
public abstract partial class PageViewModel(AuthService auth) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Error { get; set; }

    /// <summary>Runs a server action with the busy indicator and the shared error handling.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
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
        finally
        {
            IsBusy = false;
        }
    }

    protected async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        await Shell.Current.GoToAsync(Routes.Login);
    }
}
