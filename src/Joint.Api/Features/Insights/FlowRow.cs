namespace Joint.Api.Features.Insights;

/// <summary>One transaction, reduced to what the insights need.</summary>
public record FlowRow(
    Guid AccountId, Guid OwnerId, string OwnerName, string? OwnerAvatarUrl, DateOnly BookedOn, decimal Amount, string Currency);
