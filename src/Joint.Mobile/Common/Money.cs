namespace Joint.Mobile.Common;

/// <summary>Formats amounts the same way everywhere, using the phone's number format.</summary>
public static class Money
{
    public static string Format(decimal amount, string currency) => $"{amount:N2} {currency}";

    /// <summary>Like <see cref="Format"/>, with an explicit plus on money coming in.</summary>
    public static string FormatSigned(decimal amount, string currency) =>
        amount > 0 ? $"+{Format(amount, currency)}" : Format(amount, currency);
}
