namespace Joint.Mobile.Features.Insights;

/// <summary>
/// Bars per label, one bar for each series side by side. Values below zero hang under the
/// zero line, in <paramref name="negativeColor"/> when one is given.
/// </summary>
public class BarChart(IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, Color? negativeColor = null)
    : IDrawable
{
    private const float MaxBarWidth = 18;
    private const float BarGap = 3;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (labels.Count == 0 || series.Count == 0)
        {
            return;
        }

        var plot = ChartStyle.PlotArea(dirtyRect);
        var values = series.SelectMany(s => s.Values).Select(v => (float)v).ToList();
        var top = Math.Max(values.Max(), 0);
        var bottom = Math.Min(values.Min(), 0);
        if (top - bottom <= 0)
        {
            top = 1;
        }

        float ToY(float value) => plot.Bottom - ((value - bottom) / (top - bottom) * plot.Height);

        ChartStyle.DrawGrid(canvas, dirtyRect, plot, [top, top / 2, 0, bottom], ToY);

        var groupWidth = plot.Width / labels.Count;
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

            canvas.FontColor = ChartStyle.Muted;
            canvas.FontSize = ChartStyle.FontSize;
            canvas.DrawString(
                labels[group], groupLeft, plot.Bottom + 4, groupWidth, ChartStyle.LabelHeight - 4,
                HorizontalAlignment.Center, VerticalAlignment.Top);
        }
    }
}
