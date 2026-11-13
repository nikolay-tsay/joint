using System.Text.RegularExpressions;

namespace Joint.Api.Features.Recurring;

/// <summary>
/// Works out the text that identifies a payee. A tracked payment counts every transaction
/// whose counterparty or description contains its match text, so the text has to be the
/// part that stays the same from one payment to the next. This class makes the first
/// guess; the user can correct it when marking.
/// </summary>
public static partial class PaymentKey
{
    public const int MaxLength = 200;

    /// <summary>Anything shorter would match half the list.</summary>
    public const int MinLength = 3;

    /// <summary>A word with a digit in it is taken for a reference once it is this long.</summary>
    private const int ShortestReference = 4;

    /// <summary>The suggested match text, or null when the transaction names nobody.</summary>
    public static string? From(string? counterparty, string? description) =>
        NameOf(counterparty, description)?.ToUpperInvariant();

    /// <summary>
    /// The payee as it should be shown: the counterparty when the bank gives one, otherwise
    /// the merchant part of the description. Fio writes card payments as
    /// "Nákup: NETFLIX.COM, Los Gatos, dne ...", where only the text up to the first comma
    /// names the merchant. Payment references are dropped either way: Spotify, for one,
    /// puts a new id after its name every month.
    /// </summary>
    public static string? NameOf(string? counterparty, string? description)
    {
        var name = Tidy(counterparty) ?? Tidy(MerchantPart(description));
        if (name is null)
        {
            return null;
        }

        var words = name.Split(' ').Where(word => !LooksLikeReference(word)).ToList();
        if (words.Count > 0)
        {
            name = string.Join(' ', words);
        }

        return name.Length > MaxLength ? name[..MaxLength] : name;
    }

    /// <summary>Whether a transaction's counterparty or description contains the match text.</summary>
    public static bool Matches(string matchText, string? counterparty, string? description) =>
        $"{Tidy(counterparty)} {Tidy(description)}".Contains(matchText, StringComparison.OrdinalIgnoreCase);

    /// <summary>Match text as it is stored: trimmed, single-spaced, upper case.</summary>
    public static string? Normalize(string? matchText) => Tidy(matchText)?.ToUpperInvariant();

    // "O2" and "212" are parts of names; "P2F3A9C1" and "0123" are not.
    private static bool LooksLikeReference(string word) =>
        word.Length >= ShortestReference && word.Any(char.IsDigit);

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
