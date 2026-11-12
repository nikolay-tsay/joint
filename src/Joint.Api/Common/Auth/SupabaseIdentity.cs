using System.Security.Claims;
using System.Text.Json;

namespace Joint.Api.Common.Auth;

/// <summary>What a Supabase access token says about the signed-in person.</summary>
public record SupabaseIdentity(Guid UserId, string Email, string DisplayName)
{
    public static bool TryRead(ClaimsPrincipal principal, out SupabaseIdentity identity)
    {
        identity = null!;
        if (!Guid.TryParse(principal.FindFirstValue("sub"), out var userId))
        {
            return false;
        }

        var email = principal.FindFirstValue("email") ?? "";
        identity = new SupabaseIdentity(userId, email, ReadDisplayName(principal, email));
        return true;
    }

    /// <summary>
    /// Prefers the name from the sign-in provider (in the <c>user_metadata</c> claim),
    /// then the part of the email before the @.
    /// </summary>
    private static string ReadDisplayName(ClaimsPrincipal principal, string email)
    {
        var metadata = principal.FindFirstValue("user_metadata");
        if (!string.IsNullOrWhiteSpace(metadata))
        {
            try
            {
                using var json = JsonDocument.Parse(metadata);
                foreach (var key in (string[])["full_name", "name"])
                {
                    if (json.RootElement.ValueKind == JsonValueKind.Object
                        && json.RootElement.TryGetProperty(key, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString()!.Trim();
                    }
                }
            }
            catch (JsonException)
            {
                // Metadata is optional and user-controlled; fall back to the email.
            }
        }

        var at = email.IndexOf('@');
        return at > 0 ? email[..at] : "User";
    }
}
