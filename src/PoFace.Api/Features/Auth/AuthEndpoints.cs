using System.Security.Claims;

namespace PoFace.Api.Features.Auth;

/// <summary>Maps authentication-info endpoints for the WASM client to call after login.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/auth/me — returns the authenticated caller's identity.
        app.MapGet("/api/auth/me", (ClaimsPrincipal user) =>
        {
            var userId      = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var displayName = user.FindFirstValue("name")
                           ?? user.FindFirstValue(ClaimTypes.Name)
                           ?? userId;

            return Results.Ok(new { userId, displayName });
        })
        .RequireAuthorization()
        .WithName("AuthMe");

        return app;
    }
}
