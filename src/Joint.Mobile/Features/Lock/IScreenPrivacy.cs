namespace Joint.Mobile.Features.Lock;

/// <summary>Keeps the app's contents out of the system's app switcher.</summary>
public interface IScreenPrivacy
{
    /// <summary>Hides or shows the app's preview in the app switcher.</summary>
    void SetHidden(bool hidden);
}
