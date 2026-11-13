using System.Text.RegularExpressions;

namespace Joint.Api.Features.Categories;

/// <summary>
/// Which categories there are. A starter list is always offered; a household adds its own
/// simply by using a new name, and that name is offered for as long as a rule uses it.
/// </summary>
public static partial class CategoryNames
{
    public const int MaxLength = 50;

    /// <summary>Offered to everyone, in this order.</summary>
    public static readonly IReadOnlyList<string> Starter =
    [
        "Groceries", "Eating out", "Transport", "Housing", "Utilities", "Health", "Shopping",
        "Entertainment", "Travel", "Kids", "Gifts", "Fees", "Other",
    ];

    /// <summary>The starter list followed by the household's own names, alphabetically.</summary>
    public static List<string> Available(IEnumerable<string> used) =>
        Starter
            .Concat(used
                .Where(name => !Starter.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
            .ToList();

    /// <summary>
    /// Tidies a name someone typed and, when it is an existing category in other
    /// capitals, returns that one's spelling, so "groceries" does not become a second category.
    /// </summary>
    /// <returns>Null when nothing usable was typed.</returns>
    public static string? Canonical(string? typed, IEnumerable<string> used)
    {
        if (string.IsNullOrWhiteSpace(typed))
        {
            return null;
        }

        var name = Whitespace().Replace(typed.Trim(), " ");
        if (name.Length > MaxLength)
        {
            return null;
        }

        return Available(used).FirstOrDefault(known => string.Equals(known, name, StringComparison.OrdinalIgnoreCase)) ?? name;
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
