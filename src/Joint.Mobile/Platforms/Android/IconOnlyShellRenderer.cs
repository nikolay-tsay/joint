using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Joint.Mobile;

/// <summary>
/// Shows the bottom tabs as icons only. Shell has no setting for this, and clearing the
/// titles would leave the space for them and take away what a screen reader announces.
/// </summary>
public class IconOnlyShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new IconOnlyTracker(this, shellItem);

    private sealed class IconOnlyTracker(IShellContext shellContext, ShellItem shellItem)
        : ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
    {
        public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            base.SetAppearance(bottomView, appearance);
            bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityUnlabeled;
        }
    }
}
