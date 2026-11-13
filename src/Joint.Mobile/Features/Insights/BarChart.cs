namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Bars per label, one bar for each series side by side. Values below zero hang under the
/// zero line, in <paramref name="negativeColor"/> when one is given. The picked label gets
/// a shaded band behind its bars.
/// </summary>
public class BarChart(IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, Color? negativeColor = null)
    : Chart
{
    private const float MaxBarWidth = 18;
    private const float BarGap = 3;

    protected override int? IndexAt(PointF point)
    {
        var x = point.X;
        var plot = ChartStyle.PlotArea(Bounds);
        if (labels.Count == 0 || plot.Width <= 0)
        {
            return null;
        }

        // A touch on the axis numbers or just off the edge picks the nearest end.
        return Math.Clamp((int)((x - plot.Left) / (plot.Width / labels.Count)), 0, labels.Count - 1);
    }

    protected override void Render(ICanvas canvas, RectF bounds)
    {
        if (labels.Count == 0 || series.Count == 0)
        {
            return;
        }

        var plot = ChartStyle.PlotArea(bounds);
        var values = series.SelectMany(s => s.Values).Select(v => (float)v).ToList();
        var top = Math.Max(values.Max(), 0);
        var bottom = Math.Min(values.Min(), 0);
        if (top - bottom <= 0)
        {
            top = 1;
        }

        float ToY(float value) => plot.Bottom - ((value - bottom) / (top - bottom) * plot.Height);

        var groupWidth = plot.Width / labels.Count;
        if (SelectedIndex is { } picked)
        {
            canvas.FillColor = ChartStyle.Grid;
            canvas.FillRoundedRectangle(plot.Left + (picked * groupWidth), plot.Top, groupWidth, plot.Height, 6);
        }

        ChartStyle.DrawGrid(canvas, bounds, plot, [top, top / 2, 0, bottom], ToY);

        var barWidth = Math.Min(MaxBarWidth, ((groupWidth * 0.7f) - (BarGap * (series.Count - 1))) / series.Count);
        var barsWidth = (barWidth * series.Count) + (BarGap * (series.Count - 1));

        for (var group = 0; group < labels.Count; group++)
        {
            var groupLeft = plot.Left + (group * groupWidth);
            var x = groupLeft + ((groupWidth - barsWidth) / 2);
            foreach (var one in series)
            {
                var value = group < one.Values.Count ? (float)one.Values[group] : 0;
                if (value != 0)
                {
                    var from = ToY(Math.Max(value, 0));
                    var height = Math.Max(Math.Abs(ToY(value) - ToY(0)), 2);
                    canvas.FillColor = value < 0 && negativeColor is not null ? negativeColor : one.Color;
                    canvas.FillRoundedRectangle(x, from, barWidth, height, 3);
                }

                x += barWidth + BarGap;
            }

            canvas.FontColor = group == SelectedIndex ? ChartStyle.Spending : ChartStyle.Muted;
            canvas.FontSize = ChartStyle.FontSize;
            canvas.DrawString(
                labels[group], groupLeft, plot.Bottom + 4, groupWidth, ChartStyle.LabelHeight - 4,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
