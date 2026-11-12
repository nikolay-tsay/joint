using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Joint.Api.Common;

public static class SupabaseAuth
{
    /// <summary>
    /// Supabase Auth issues the tokens; the API only validates them against the project's signing keys.
    /// </summary>
    public static IServiceCollection AddSupabaseAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var supabaseUrl = configuration["Supabase:Url"]
            ?? throw new InvalidOperationException("Supabase:Url is not configured.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = $"{supabaseUrl.TrimEnd('/')}/auth/v1";
                options.Audience = "authenticated";
                options.MapInboundClaims = false;
            });
        services.AddAuthorization();

        return services;
    }
}
