using MediatR;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PoFace.Api.Features.GameSession;

/// <summary>Maps the game-session management endpoints to MediatR commands.</summary>
public static class GameSessionEndpoints
{
    public static IEndpointRouteBuilder MapGameSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/sessions — start a new session
        app.MapPost("/api/sessions", HandleStartSessionAsync)
           .RequireAuthorization();

        // POST /api/sessions/{id}/complete — mark session done, compute total
        app.MapPost("/api/sessions/{sessionId}/complete", HandleCompleteSessionAsync)
           .RequireAuthorization();

        // DELETE /api/sessions/{id} — discard an in-progress session
        app.MapDelete("/api/sessions/{sessionId}", HandleDiscardSessionAsync)
           .RequireAuthorization();

        return app;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> HandleStartSessionAsync(
        IMediator mediator,
        ClaimsPrincipal user,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var userId      = GetUserId(user);
        var displayName = user.FindFirstValue("name")
                       ?? user.FindFirstValue(ClaimTypes.Name)
                       ?? userId;
        var deviceType  = DetectDeviceType(request);

        var result = await mediator.Send(
            new StartSessionCommand(userId, displayName, deviceType), cancellationToken);

        return Results.Created(
            $"/api/sessions/{result.SessionId}",
            new
            {
                sessionId = result.SessionId,
                rounds    = result.Rounds.Select(r => new
                {
                    roundNumber   = r.RoundNumber,
                    targetEmotion = r.TargetEmotion
                })
            });
    }

    private static async Task<IResult> HandleCompleteSessionAsync(
        string sessionId,
        IMediator mediator,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId(user);

        try
        {
            var result = await mediator.Send(
                new CompleteSessionCommand(sessionId, userId), cancellationToken);

            return Results.Ok(new
            {
                sessionId      = result.SessionId,
                totalScore     = result.TotalScore,
                isPersonalBest = result.IsPersonalBest,
                recapUrl       = result.RecapUrl
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static Task<IResult> HandleDiscardSessionAsync(
        string sessionId,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        // T031 spec: "No storage writes occur" on discard.
        // In Phase 3 we simply return 204; cleanup is handled by TTL/expiry in Phase 4.
        _ = sessionId;
        _ = user;
        return Task.FromResult(Results.NoContent());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GetUserId(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? user.FindFirstValue("sub")
        ?? "unknown";

    private static string DetectDeviceType(HttpRequest request)
    {
        var ua = request.Headers.UserAgent.ToString();
        return (ua.Contains("Mobi", StringComparison.OrdinalIgnoreCase) ||
                ua.Contains("Android", StringComparison.OrdinalIgnoreCase))
            ? "Mobile"
            : "Desktop";
    }
}
