using MediatR;

namespace PoFace.Api.Features.Leaderboard;

/// <summary>Maps the public leaderboard read endpoint.</summary>
public static class LeaderboardEndpoints
{
    public static IEndpointRouteBuilder MapLeaderboardEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/leaderboard?top={n} — no auth, public read.
        app.MapGet("/api/leaderboard", async (
            ISender      sender,
            int          top = 100,
            CancellationToken cancellationToken = default) =>
        {
            var clamped = Math.Clamp(top, 1, 500);
            var result  = await sender.Send(new GetLeaderboardQuery(clamped), cancellationToken);
            return Results.Ok(result);
        })
        .AllowAnonymous()
        .WithName("GetLeaderboard");

        return app;
    }
}
