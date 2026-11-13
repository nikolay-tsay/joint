using Joint.Contracts;
using Joint.Mobile.Common;

namespace Joint.Mobile.Features.Insights;

/// <summary>One household member's spending this month, formatted for display.</summary>
/// <param name="Share">Their spending against the biggest spender's, 0 to 1, for the bar.</param>
public record MemberSpendingItem(string Name, string Initial, string? AvatarUrl, string Amount, double Share)
{
    public static MemberSpendingItem From(MemberSpendingResponse member, decimal biggest, string currency) =>
        new(
            member.DisplayName,
            member.DisplayName.Length > 0 ? member.DisplayName[..1].ToUpperInvariant() : "?",
            member.AvatarUrl,
            Money.Format(member.Spending, currency),
            biggest > 0 ? (double)(member.Spending / biggest) : 0);
}
