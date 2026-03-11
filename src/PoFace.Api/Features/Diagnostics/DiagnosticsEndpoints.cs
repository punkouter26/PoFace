using MediatR;

namespace PoFace.Api.Features.Diagnostics;

public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/diag", async (ISender sender, CancellationToken ct) =>
        {
            var report = await sender.Send(new DiagnosticsQuery(), ct);
            return Results.Ok(report);
        })
        .AllowAnonymous()
        .WithName("GetDiagnostics");

        return app;
    }
}
