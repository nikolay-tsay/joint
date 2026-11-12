using System.Security.Cryptography;
using System.Text;

namespace Joint.Api.Features.Households;

/// <summary>Invitation codes: short enough to read out or type, stored only as a hash.</summary>
public static class InvitationCode
{
    // No 0/O, 1/I/L: they are easy to confuse when a code is read aloud or retyped.
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";
    private const int Length = 8;

    /// <summary>A new random code, formatted like <c>ABCD-EFGH</c>.</summary>
    public static string Generate()
    {
        var code = RandomNumberGenerator.GetString(Alphabet, Length);
        return $"{code[..4]}-{code[4..]}";
    }

    /// <summary>The stored form of a code. Ignores case, spaces and dashes in what the user typed.</summary>
    public static string Hash(string code)
    {
        var normalized = new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }
}
