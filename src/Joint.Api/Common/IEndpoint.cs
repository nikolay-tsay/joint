namespace Joint.Api.Common;

/// <summary>One HTTP endpoint. Each feature slice implements this next to its own logic.</summary>
public interface IEndpoint
{
    static abstract void Map(IEndpointRouteBuilder app);
}

public static class EndpointExtensions
{
    public static IEndpointRouteBuilder MapEndpoint<TEndpoint>(this IEndpointRouteBuilder app)
        where TEndpoint : IEndpoint
    {
        TEndpoint.Map(app);
        return app;
    }
}
