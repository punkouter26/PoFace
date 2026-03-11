using MediatR;
using Microsoft.AspNetCore.Mvc;
using PoFace.Api.Features.Recap;

namespace PoFace.Api.Features.Scoring;

/// <summary>Maps the POST scoring endpoint to the MediatR command pipeline.</summary>
public static class ScoringEndpoints
{
    private const long MaxImageBytes = 500 * 1024; // 500 KB

    public static IEndpointRouteBuilder MapScoringEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(
            "/api/sessions/{sessionId}/rounds/{roundNumber}/score",
            HandleScoreRoundAsync)
           .AllowAnonymous()
           .DisableAntiforgery(); // multipart handled manually; CSRF not applicable to API

        return app;
    }

    private static async Task<IResult> HandleScoreRoundAsync(
        string sessionId,
        int roundNumber,
        IFormFile? image,
        IMediator mediator,
        IGameSessionLookupService sessionLookup,
        CancellationToken cancellationToken)
    {
        // ── Validation ────────────────────────────────────────────────────────

        if (roundNumber is < 1 or > 5)
            return Results.UnprocessableEntity(new ProblemDetails
            {
                Title  = "Invalid round number",
                Detail = "Round number must be between 1 and 5.",
                Status = StatusCodes.Status422UnprocessableEntity
            });

        if (image is null)
            return Results.UnprocessableEntity(new ProblemDetails
            {
                Title  = "Missing image",
                Detail = "A multipart field named 'image' is required.",
                Status = StatusCodes.Status422UnprocessableEntity
            });

        if (!string.Equals(image.ContentType, "image/jpeg", StringComparison.OrdinalIgnoreCase))
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (image.Length > MaxImageBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        // ── Build command ─────────────────────────────────────────────────────

        byte[] imageBytes;
        using (var ms = new MemoryStream())
        {
            await image.CopyToAsync(ms, cancellationToken);
            imageBytes = ms.ToArray();
        }

        var session = await sessionLookup.GetBySessionIdAsync(sessionId, cancellationToken);
        if (session is null)
            return Results.NotFound();

        // Derive target emotion from round number (canonical order per FR-009).
        var targetEmotion = RoundEmotions.ForRound(roundNumber);

        var command = new ScoreRoundCommand(
            sessionId, session.UserId, roundNumber, targetEmotion, imageBytes);

        var result = await mediator.Send(command, cancellationToken);

        return Results.Ok(result);
    }
}

/// <summary>Canonical 5-round emotion sequence (FR-009 — order MUST NOT be shuffled).</summary>
internal static class RoundEmotions
{
    private static readonly string[] Order =
        ["Happiness", "Surprise", "Anger", "Sadness", "Fear"];

    public static string ForRound(int roundNumber) => Order[roundNumber - 1];
}
