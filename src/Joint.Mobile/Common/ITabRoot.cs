namespace Joint.Mobile.Common;

/// <summary>
/// A page at the root of a tab. Tapping the tab while already on it calls
/// <see cref="ResetView"/>, which takes the page back to how it opens: scrolled to the top,
/// on its first view. See <c>IconOnlyShellRenderer</c>, which notices the second tap.
/// </summary>
public interface ITabRoot
{
    void ResetView();
}
