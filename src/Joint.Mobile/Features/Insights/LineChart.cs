namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Lines over the days of a month, the first series underneath. The last series is the one
/// being followed, so it ends in a dot.
/// </summary>
public class LineChart(IReadOnlyList<ChartSeries> series) : IDrawable
{
    /// <summary>Days labelled under the plot.</summary>
    private static readonly int[] LabelledDays = [1, 10, 20, 30];

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var days = series.Count == 0 ? 0 : series.Max(s => s.Values.Count);
        if (days < 2)
        {
            return;
        }

        var plot = ChartStyle.PlotArea(dirtyRect);
        var top = Math.Max((float)series.SelectMany(s => s.Values).DefaultIfEmpty().Max(), 1);

        float ToY(float value) => plot.Bottom - (value / top * plot.Height);
        float ToX(int index) => plot.Left + (index / (float)(days - 1) * plot.Width);

        ChartStyle.DrawGrid(canvas, dirtyRect, plot, [top, top / 2, 0], ToY);

        canvas.FontColor = ChartStyle.Muted;
        canvas.FontSize = ChartStyle.FontSize;
        foreach (var day in LabelledDays.Where(d => d <= days))
        {
            canvas.DrawString(
                day.ToString(), ToX(day - 1) - 15, plot.Bottom + 4, 30, ChartStyle.LabelHeight - 4,
                HorizontalAlignment.Center, VerticalAlignment.Top);
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
        }

        if (series[^1] is { Values.Count: > 0 } followed)
        {
            canvas.FillColor = followed.Color;
            canvas.FillCircle(ToX(followed.Values.Count - 1), ToY((float)followed.Values[^1]), 4.5f);
        }
    }
}
