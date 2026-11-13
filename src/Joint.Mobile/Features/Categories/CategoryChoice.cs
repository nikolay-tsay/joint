namespace Joint.Mobile.Features.Categories;

/// <summary>One category in the picker's list.</summary>
/// <param name="IsCurrent">True for the category the payment already has.</param>
public record CategoryChoice(string Name, bool IsCurrent);
