using Joint.Contracts;

namespace Joint.Mobile.Features.Profile;

/// <summary>One household member on the profile page.</summary>
public record MemberItem(string Name, string Initial, string? AvatarUrl)
{
    public static MemberItem From(HouseholdMemberResponse member, bool isCurrentUser) =>
        new(
            isCurrentUser ? $"{member.DisplayName} (you)" : member.DisplayName,
            member.DisplayName.Length > 0 ? member.DisplayName[..1].ToUpperInvariant() : "?",
            member.AvatarUrl);
}
