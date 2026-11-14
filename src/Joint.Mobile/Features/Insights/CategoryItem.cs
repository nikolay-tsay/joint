namespace Joint.Mobile.Features.Insights;

/// <summary>One slice of the category chart as its legend shows it.</summary>
/// <param name="Color">The slice's color, repeated in the dot in front of the name.</param>
/// <param name="Percent">The slice's share of the month's spending.</param>
/// <param name="IsUncategorized">The slice for spending no category matches, rather than a category of that name.</param>
public record CategoryItem(string Name, string Amount, string Percent, Color Color, bool IsUncategorized = false);
