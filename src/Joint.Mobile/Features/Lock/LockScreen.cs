namespace Joint.Mobile.Features.Lock;

/// <summary>
/// Puts the lock page in front of the app when it should be locked: at launch, and every
/// time the app comes back from the background, however briefly it was away.
/// </summary>
public class LockScreen(AppLock appLock, IServiceProvider services)
{
    private Task<bool>? _showing;
    private bool _wasBackgrounded;

    /// <summary>Call when the app goes to the background.</summary>
    public void NoteBackgrounded() => _wasBackgrounded = true;

    /// <summary>Call when the app returns to the foreground.</summary>
    /// <returns>False when the user ended up signed out and belongs on the login page.</returns>
    public async Task<bool> OnResumedAsync()
    {
        var wasAway = _wasBackgrounded;
        _wasBackgrounded = false;
        return _showing is not null || !wasAway || await RequireUnlockAsync();
    }

    /// <summary>
    /// Shows the lock page, if the lock is on, and waits until the user is through.
    /// </summary>
    /// <returns>False when the user signed out instead, by choice or by too many wrong PINs.</returns>
    public async Task<bool> RequireUnlockAsync()
    {
        if (_showing is not null)
        {
            return await _showing;
        }

        await appLock.ApplyScreenPrivacyAsync();
        if (!await appLock.IsEnabledAsync())
        {
            return true;
        }

        _showing = ShowAsync();
        try
        {
            return await _showing;
        }
        finally
        {
            _showing = null;
        }
    }

    /// <summary>
    /// Offers the app lock right after signing in, when it is not on yet. Returns once the
    /// user has turned it on or said "not now". It is asked once per sign-in and never at
    /// launch; signing out removes the lock, so the next sign-in asks again.
    /// </summary>
    public async Task OfferSetupAsync()
    {
        if (await appLock.IsEnabledAsync())
        {
            return;
        }

        var page = services.GetRequiredService<LockSetupPage>();
        var navigation = Shell.Current.Navigation;
        await navigation.PushModalAsync(page, animated: true);
        await page.WaitAsync();
        await navigation.PopModalAsync(animated: true);
    }

    private async Task<bool> ShowAsync()
    {
        var page = services.GetRequiredService<LockPage>();
        var navigation = Shell.Current.Navigation;
        await navigation.PushModalAsync(page, animated: false);
        var unlocked = await page.WaitAsync();
        await navigation.PopModalAsync(animated: false);
        return unlocked;
    }
}
