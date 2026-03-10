using MediatR;

namespace PoFace.Api.Features.Recap;

/// <summary>Maps the public recap endpoint.</summary>
public static class RecapEndpoints
{
    public static IEndpointRouteBuilder MapRecapEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/recap/{sessionId} — no auth; publicly shareable recap link.
        app.MapGet("/api/recap/{sessionId}", HandleGetRecapAsync)
           .AllowAnonymous()
           .WithName("GetRecap");

        return app;
    }

    private static async Task<IResult> HandleGetRecapAsync(
        string            sessionId,
        ISender           sender,
        HttpResponse      response,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(new GetRecapQuery(sessionId), cancellationToken);

        return result.Status switch
        {
            RecapStatus.NotFound => Results.NotFound(),

            RecapStatus.Gone => Results.Problem(
                statusCode: 410,
                title:      "Gone",
                detail:     "This recap has expired and is no longer available."),

            _ => ServeRecap(result.Recap!)
        };
    }

    /// <summary>Sets appropriate Cache-Control headers and returns the recap payload.</summary>
    private static IResult ServeRecap(RecapDto recap) => new RecapJsonResult(recap);

    private sealed class RecapJsonResult(RecapDto recap) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.CacheControl = recap.IsPersonalBest
                ? "public, max-age=3600"
                : "private, no-store";

            await httpContext.Response.WriteAsJsonAsync(recap);
        }
    }
}
