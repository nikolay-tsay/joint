using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.Controls.Handlers.Compatibility;
using Microsoft.Maui.Controls.Platform.Compatibility;

namespace Joint.Mobile;

/// <summary>
/// Shows the bottom tabs as icons only, with the current tab's icon a little larger. Shell
/// has no setting for either, and clearing the titles would leave the space for them and
/// take away what a screen reader announces.
/// </summary>
public class IconOnlyShellRenderer : ShellRenderer
{
    protected override IShellBottomNavViewAppearanceTracker CreateBottomNavViewAppearanceTracker(ShellItem shellItem) =>
        new IconOnlyTracker(this, shellItem);

    private sealed class IconOnlyTracker(IShellContext shellContext, ShellItem shellItem)
        : ShellBottomNavViewAppearanceTracker(shellContext, shellItem)
    {
        /// <summary>How much larger the current tab's icon is drawn.</summary>
        private const float CurrentTabScale = 1.3f;

        private readonly Shell _shell = shellContext.Shell;
        private BottomNavigationView? _bottomView;

        public override void SetAppearance(BottomNavigationView bottomView, IShellAppearanceElement appearance)
        {
            base.SetAppearance(bottomView, appearance);
            bottomView.LabelVisibilityMode = LabelVisibilityMode.LabelVisibilityUnlabeled;

            if (_bottomView is null)
            {
                // Shell owns the view's own selection listener, and taking it over would
                // break tab switching; Shell's navigation event says the same thing.
                _shell.Navigated += OnNavigated;
            }

            _bottomView = bottomView;
            MagnifyCurrentTab(animated: false);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _shell.Navigated -= OnNavigated;
                _bottomView = null;
            }

            base.Dispose(disposing);
        }

        private void OnNavigated(object? sender, ShellNavigatedEventArgs e) => MagnifyCurrentTab(animated: true);

        private void MagnifyCurrentTab(bool animated)
        {
            // Posted, so it runs after the view has moved its selection to the new tab.
            _bottomView?.Post(() =>
            {
                if (_bottomView is not { Menu: { } menu } view)
                {
                    return;
                }

                for (var index = 0; index < menu.Size(); index++)
                {
                    if (menu.GetItem(index) is not { } item || view.FindViewById(item.ItemId) is not { } tab)
                    {
                        continue;
                    }

                    var scale = item.ItemId == view.SelectedItemId ? CurrentTabScale : 1f;
                    if (animated)
                    {
                        tab.Animate()?.ScaleX(scale).ScaleY(scale).SetDuration(150).Start();
                    }
                    else
                    {
                        tab.ScaleX = scale;
                        tab.ScaleY = scale;
                    }
                }
            });
        }
    }
}
