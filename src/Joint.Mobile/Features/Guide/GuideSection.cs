namespace Joint.Mobile.Features.Guide;

/// <summary>One topic of the guide: a heading, a sentence saying where it applies, and its points.</summary>
public record GuideSection(string Title, string Where, IReadOnlyList<string> Points);
