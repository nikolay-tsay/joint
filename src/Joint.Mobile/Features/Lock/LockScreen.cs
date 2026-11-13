namespace Joint.Mobile.Features.Lock;

/// <summary>
/// Puts the lock page in front of the app when it should be locked: at launch, and when
/// the app comes back after a while in the background.
/// </summary>
public class LockScreen(AppLock appLock, IServiceProvider services)
{
    /// <summary>
    /// Short trips out of the app (copying a code, answering a notification) don't lock it.
    /// </summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(1);

    private Task<bool>? _showing;
    private DateTimeOffset? _backgroundedAt;

    /// <summary>Call when the app goes to the background.</summary>
    public void NoteBackgrounded() => _backgroundedAt ??= DateTimeOffset.UtcNow;

    /// <summary>Call when the app returns to the foreground.</summary>
    /// <returns>False when the user ended up signed out and belongs on the login page.</returns>
    public async Task<bool> OnResumedAsync()
    {
        var away = DateTimeOffset.UtcNow - _backgroundedAt;
        _backgroundedAt = null;
        return _showing is not null || away is null || away < Grace || await RequireUnlockAsync();
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
