using MediatR;
using System.Security.Claims;
using PoFace.Api.Features.Recap;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.Api.Features.GameSession;

/// <summary>Maps the game-session management endpoints to MediatR commands.</summary>
public static class GameSessionEndpoints
{
    public static IEndpointRouteBuilder MapGameSessionEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/sessions — start a new session
        app.MapPost("/api/sessions", HandleStartSessionAsync)
              .AllowAnonymous();

        // POST /api/sessions/{id}/complete — mark session done, compute total
        app.MapPost("/api/sessions/{sessionId}/complete", HandleCompleteSessionAsync)
              .AllowAnonymous();

        // DELETE /api/sessions/{id} — discard an in-progress session
        app.MapDelete("/api/sessions/{sessionId}", HandleDiscardSessionAsync)
              .AllowAnonymous();

        return app;
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> HandleStartSessionAsync(
        IMediator mediator,
        ClaimsPrincipal user,
        HttpRequest request,
        CancellationToken cancellationToken)
    {
        var isAuthenticatedUser = user.Identity?.IsAuthenticated == true;
        var userId = isAuthenticatedUser ? GetUserId(user) : null;
        var displayName = isAuthenticatedUser
            ? user.FindFirstValue("name") ?? user.FindFirstValue(ClaimTypes.Name) ?? userId
            : null;
        var deviceType  = DetectDeviceType(request);

        var result = await mediator.Send(
            new StartSessionCommand(userId, displayName, deviceType, isAuthenticatedUser), cancellationToken);

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
        IGameSessionLookupService sessionLookup,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.GetBySessionIdAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NotFound();

        // Ownership guard — authenticated sessions may only be completed by their owner (OWASP A01).
        if (session.IsAuthenticatedUser)
        {
            var callerId = GetUserId(user);
            if (!string.Equals(callerId, session.UserId, StringComparison.Ordinal))
                return Results.Forbid();
        }

        try
        {
            var result = await mediator.Send(
                new CompleteSessionCommand(sessionId, session.UserId), cancellationToken);

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

    private static async Task<IResult> HandleDiscardSessionAsync(
        string sessionId,
        IGameSessionLookupService sessionLookup,
        ITableStorageService tableStorage,
        CancellationToken cancellationToken)
    {
        var session = await sessionLookup.GetBySessionIdAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NoContent(); // idempotent — session already gone

        if (!session.IsCompleted)
        {
            session.IsCompleted = true;
            session.CompletedAt = DateTimeOffset.UtcNow;
            session.ExpiresAt   = DateTimeOffset.UtcNow.Add(TimeSpan.FromHours(1));
            await tableStorage.UpsertEntityAsync("GameSessions", session, cancellationToken);
        }

        return Results.NoContent();
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
