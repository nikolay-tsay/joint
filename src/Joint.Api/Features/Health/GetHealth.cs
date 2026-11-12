using Joint.Api.Common;

namespace Joint.Api.Features.Health;

public class GetHealth : IEndpoint
{
    public static void Map(IEndpointRouteBuilder app) =>
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
}
