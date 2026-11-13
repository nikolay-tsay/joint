namespace Joint.Mobile.Features.Insights;

/// <summary>
/// A hand-drawn chart that can be touched: the touch picks the month, day or slice under the
/// finger, the chart marks it, and <see cref="Selected"/> tells whoever built the chart so
/// it can show that period's exact numbers.
/// </summary>
public abstract class Chart : IDrawable
{
    /// <summary>Where the chart was last drawn, to turn a touch into a position on it.</summary>
    protected RectF Bounds { get; private set; }

    /// <summary>The picked month or day, counted from the left; null until the chart is touched.</summary>
    public int? SelectedIndex { get; private set; }

    /// <summary>Called with the index each time the pick changes.</summary>
    public Action<int>? Selected { get; init; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        Bounds = dirtyRect;
        Render(canvas, dirtyRect);
    }

    /// <summary>Picks what is under a touch.</summary>
    /// <returns>True when the pick changed and the chart needs redrawing.</returns>
    public bool SelectAt(PointF point)
    {
        if (IndexAt(point) is not { } index || index == SelectedIndex)
        {
            return false;
        }

        SelectedIndex = index;
        Selected?.Invoke(index);
        return true;
    }

    protected abstract void Render(ICanvas canvas, RectF bounds);

    /// <summary>The month, day or slice at a point, or null when nothing is there.</summary>
    protected abstract int? IndexAt(PointF point);
}
