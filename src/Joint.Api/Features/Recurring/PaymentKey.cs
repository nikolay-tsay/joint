using System.Text.RegularExpressions;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// Decides which transactions go to the same payee. The counterparty is used when the
/// bank gives one. Otherwise the description is, reduced to the part that stays the same
/// between payments: Fio writes card payments as "Nákup: NETFLIX.COM, Los Gatos, dne ...",
/// where only the text up to the first comma names the merchant.
/// </summary>
public static partial class PaymentKey
{
    public const int MaxLength = 200;

    /// <summary>The key to match on, or null when the transaction names nobody.</summary>
    public static string? From(string? counterparty, string? description) =>
        NameOf(counterparty, description)?.ToUpperInvariant();

    /// <summary>The payee as it should be shown.</summary>
    public static string? NameOf(string? counterparty, string? description)
    {
        var name = Tidy(counterparty) ?? Tidy(MerchantPart(description));
        return name is { Length: > MaxLength } ? name[..MaxLength] : name;
    }

    private static string? MerchantPart(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var text = LeadingLabel().Replace(description.Trim(), "");
        var comma = text.IndexOf(',');
        return comma > 0 ? text[..comma] : text;
    }

    private static string? Tidy(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : Whitespace().Replace(text.Trim(), " ");

    /// <summary>A single word and a colon in front, like "Nákup: ".</summary>
    [GeneratedRegex(@"^\p{L}+:\s+")]
    private static partial Regex LeadingLabel();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
