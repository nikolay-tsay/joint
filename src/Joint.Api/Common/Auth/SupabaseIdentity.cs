using System.Security.Claims;
using System.Text.Json;

namespace Joint.Api.Common.Auth;

/// <summary>What a Supabase access token says about the signed-in person.</summary>
/// <param name="AvatarUrl">The profile picture from the sign-in provider (Google), if any.</param>
public record SupabaseIdentity(Guid UserId, string Email, string DisplayName, string? AvatarUrl)
{
    public static bool TryRead(ClaimsPrincipal principal, out SupabaseIdentity identity)
    {
        identity = null!;
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
        {
            return false;
        }

        var email = principal.FindFirstValue("email") ?? "";
        var metadata = ReadMetadata(principal);

        // Prefer the provider's name, then the part of the email before the @.
        var at = email.IndexOf('@');
        var displayName = metadata("full_name") ?? metadata("name") ?? (at > 0 ? email[..at] : "User");

        identity = new SupabaseIdentity(
            userId, email, displayName, AsPictureUrl(metadata("avatar_url") ?? metadata("picture")));
        return true;
    }

    /// <summary>
    /// A lookup into the <c>user_metadata</c> claim, which the sign-in provider fills in.
    /// It is optional and user-controlled, so anything unexpected reads as missing.
    /// </summary>
    private static Func<string, string?> ReadMetadata(ClaimsPrincipal principal)
    {
        var values = new Dictionary<string, string>();
        var raw = principal.FindFirstValue("user_metadata");
        if (!string.IsNullOrWhiteSpace(raw))
        {
            try
            {
                using var json = JsonDocument.Parse(raw);
                if (json.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in json.RootElement.EnumerateObject())
                    {
                        if (property.Value.ValueKind == JsonValueKind.String
                            && property.Value.GetString() is { } text
                            && !string.IsNullOrWhiteSpace(text))
                        {
                            values[property.Name] = text.Trim();
                        }
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through with no metadata.
            }
        }

        return key => values.GetValueOrDefault(key);
    }

    /// <summary>Only a plain https link is accepted; clients load this address directly.</summary>
    private static string? AsPictureUrl(string? value) =>
        value is { Length: <= 2048 }
        && Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
            ? value
            : null;
}
