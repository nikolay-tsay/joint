using System.Globalization;

namespace Joint.Mobile.Common;

/// <summary>Formats amounts the same way everywhere, using the phone's number format.</summary>
public static class Money
{
    public static string Format(decimal amount, string currency) => $"{amount:N2} {currency}";

    /// <summary>Like <see cref="Format"/>, with an explicit plus on money coming in.</summary>
    public static string FormatSigned(decimal amount, string currency) =>
        amount > 0 ? $"+{Format(amount, currency)}" : Format(amount, currency);

    /// <summary>An amount as it goes into a text field for editing: no currency, no grouping.</summary>
    public static string ForInput(decimal amount) => amount.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Reads an amount typed with either a comma or a point, and with or without spaces.</summary>
    public static bool TryRead(string? text, out decimal amount)
    {
        var cleaned = (text ?? "").Replace(" ", "").Replace(" ", "");
        return decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.CurrentCulture, out amount)
            || decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
    }
}
