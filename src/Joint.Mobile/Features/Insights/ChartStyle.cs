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

    /// <summary>The brand orange, for a second series next to the purple.</summary>
    public static Color Accent => (Color)Application.Current!.Resources["Accent"];

    /// <summary>
    /// Colors for charts with many parts, such as the category ring: the brand purple and
    /// orange first, then hues chosen to stay apart from each other in both themes.
    /// </summary>
    public static readonly IReadOnlyList<Color> Palette =
    [
        Color.FromArgb("#7C3AED"), Color.FromArgb("#F97316"), Color.FromArgb("#0EA5E9"), Color.FromArgb("#10B981"),
        Color.FromArgb("#EAB308"), Color.FromArgb("#EC4899"), Color.FromArgb("#14B8A6"), Color.FromArgb("#F43F5E"),
        Color.FromArgb("#84CC16"), Color.FromArgb("#6366F1"), Color.FromArgb("#D946EF"), Color.FromArgb("#06B6D4"),
        Color.FromArgb("#A16207"),
    ];

    /// <summary>For the part of a chart that belongs to no category.</summary>
    public static Color Unassigned => Color.FromArgb("#9CA3AF");

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
