namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Lines over the days of a month, the first series underneath. The last series is the one
/// being followed, so it ends in a dot. The picked day gets a vertical line with a dot on
/// each series that reaches it.
/// </summary>
public class LineChart(IReadOnlyList<ChartSeries> series) : Chart
{
    /// <summary>Days labelled under the plot.</summary>
    private static readonly int[] LabelledDays = [1, 10, 20, 30];

    private int Days => series.Count == 0 ? 0 : series.Max(s => s.Values.Count);

    protected override int? IndexAt(float x)
    {
        var plot = ChartStyle.PlotArea(Bounds);
        if (Days < 2 || plot.Width <= 0)
        {
            return null;
        }

        return Math.Clamp((int)MathF.Round((x - plot.Left) / plot.Width * (Days - 1)), 0, Days - 1);
    }

    protected override void Render(ICanvas canvas, RectF bounds)
    {
        var days = Days;
        if (days < 2)
        {
            return;
        }

        var plot = ChartStyle.PlotArea(bounds);
        var top = Math.Max((float)series.SelectMany(s => s.Values).DefaultIfEmpty().Max(), 1);

        float ToY(float value) => plot.Bottom - (value / top * plot.Height);
        float ToX(int index) => plot.Left + (index / (float)(days - 1) * plot.Width);

        ChartStyle.DrawGrid(canvas, bounds, plot, [top, top / 2, 0], ToY);

        canvas.FontColor = ChartStyle.Muted;
        canvas.FontSize = ChartStyle.FontSize;
        foreach (var day in LabelledDays.Where(d => d <= days))
        {
            canvas.DrawString(
                day.ToString(), ToX(day - 1) - 15, plot.Bottom + 4, 30, ChartStyle.LabelHeight - 4,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }

        if (SelectedIndex is { } picked)
        {
            canvas.StrokeColor = ChartStyle.Muted;
            canvas.StrokeSize = 1;
            canvas.DrawLine(ToX(picked), plot.Top, ToX(picked), plot.Bottom);
        }

        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        foreach (var one in series)
        {
            if (one.Values.Count == 0)
            {
                continue;
            }

            var path = new PathF();
            path.MoveTo(ToX(0), ToY((float)one.Values[0]));
            for (var index = 1; index < one.Values.Count; index++)
            {
                path.LineTo(ToX(index), ToY((float)one.Values[index]));
            }

            canvas.StrokeColor = one.Color;
            canvas.StrokeSize = 2.5f;
            canvas.DrawPath(path);

            if (SelectedIndex is { } day && day < one.Values.Count)
            {
                canvas.FillColor = one.Color;
                canvas.FillCircle(ToX(day), ToY((float)one.Values[day]), 4.5f);
            }
        }

        if (series[^1] is { Values.Count: > 0 } followed)
        {
            canvas.FillColor = followed.Color;
            canvas.FillCircle(ToX(followed.Values.Count - 1), ToY((float)followed.Values[^1]), 4.5f);
        }
    }
}
