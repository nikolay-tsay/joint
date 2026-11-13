namespace Joint.Mobile.Common;

/// <summary>
/// For a text field in a panel that opens in place: bind the field's IsVisible to the same
/// flag as its panel, and the keyboard comes up when the panel opens and goes away when it
/// closes. Meant for fields inside templates, which a page cannot reach by name.
/// </summary>
public class FocusWhenShownBehavior : Behavior<Entry>
{
    protected override void OnAttachedTo(Entry entry)
    {
        base.OnAttachedTo(entry);
        entry.PropertyChanged += OnEntryChanged;
    }

    protected override void OnDetachingFrom(Entry entry)
    {
        entry.PropertyChanged -= OnEntryChanged;
        base.OnDetachingFrom(entry);
    }

    private static void OnEntryChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(VisualElement.IsVisible) || sender is not Entry entry)
        {
            return;
        }

        var isShown = entry.IsVisible;

        // After the layout pass that shows or hides the panel, so the field can take focus.
        entry.Dispatcher.Dispatch(async () =>
        {
            if (isShown)
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
}
