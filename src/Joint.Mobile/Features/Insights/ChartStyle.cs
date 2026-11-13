using System.Globalization;

namespace Joint.Mobile.Features.Insights;

/// <summary>
/// What the hand-drawn charts share: theme colors, axis number format and the grid.
/// Colors are read when a chart is built, so a chart keeps its theme until the next reload.
/// </summary>
public static class ChartStyle
{
    /// <summary>Room on the left for the axis numbers.</summary>
    public const float AxisWidth = 40;

    /// <summary>Room at the bottom for the labels under the plot.</summary>
    public const float LabelHeight = 22;

    public const float FontSize = 11;

    public static Color Income => Themed("Positive", "PositiveDark");

    public static Color Spending => Themed("Primary", "PrimaryDark");

    public static Color Negative => Themed("Negative", "NegativeDark");

    public static Color Muted => Themed("TextSecondary", "TextSecondaryDark");

    public static Color Grid => Themed("InsetStroke", "InsetStrokeDark");

    /// <summary>The area the marks are drawn in, inside the axis and label margins.</summary>
    public static RectF PlotArea(RectF bounds) =>
        new(bounds.Left + AxisWidth, bounds.Top + 8, bounds.Width - AxisWidth - 4, bounds.Height - 8 - LabelHeight);

    /// <summary>Horizontal lines with their values, at the given amounts.</summary>
    public static void DrawGrid(ICanvas canvas, RectF bounds, RectF plot, IEnumerable<float> values, Func<float, float> toY)
    {
        canvas.StrokeSize = 1;
        canvas.StrokeColor = Grid;
        canvas.FontColor = Muted;
        canvas.FontSize = FontSize;
        foreach (var value in values.Distinct())
        {
            var y = toY(value);
            canvas.DrawLine(plot.Left, y, plot.Right, y);
            canvas.DrawString(
                Compact(value), bounds.Left, y - 8, AxisWidth - 6, 16, HorizontalAlignment.Right, VerticalAlignment.Center);
        }
    }

    /// <summary>12 400 becomes "12k", 1 250 000 becomes "1.3M": axis numbers have little room.</summary>
    public static string Compact(float value)
    {
        var size = Math.Abs(value);
        return size switch
        {
            >= 1_000_000 => (value / 1_000_000).ToString("0.#", CultureInfo.CurrentCulture) + "M",
            >= 10_000 => (value / 1_000).ToString("0", CultureInfo.CurrentCulture) + "k",
            >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.CurrentCulture) + "k",
            _ => value.ToString("0", CultureInfo.CurrentCulture),
        };
    }

    private static Color Themed(string light, string dark)
    {
        var app = Application.Current!;
        return (Color)app.Resources[app.RequestedTheme == AppTheme.Dark ? dark : light];
    }
}
