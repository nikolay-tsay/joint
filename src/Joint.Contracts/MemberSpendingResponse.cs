namespace Joint.Contracts;

/// <summary>What left one household member's accounts in the current month, as a positive number.</summary>
public record MemberSpendingResponse(string DisplayName, string? AvatarUrl, decimal Spending);
