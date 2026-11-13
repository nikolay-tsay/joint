namespace Joint.Api.Features.Categories;

/// <summary>One transaction, reduced to what the category breakdown needs.</summary>
public record CategoryRow(
    Guid AccountId, DateOnly BookedOn, decimal Amount, string Currency, string? Counterparty, string? Description);
