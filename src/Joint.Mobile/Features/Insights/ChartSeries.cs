namespace Joint.Mobile.Features.Insights;

/// <summary>One set of values in a chart, drawn in one color.</summary>
public record ChartSeries(IReadOnlyList<decimal> Values, Color Color);
