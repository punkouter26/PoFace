namespace PoFace.Api.Features.Auth;

/// <summary>
/// Developer-only endpoint that sets test-auth cookies for browser-based development flows.
/// Returns 403 Forbidden in the Production environment — safety guard enforced at runtime.
/// </summary>
public static class DevLoginEndpoints
{
    public static IEndpointRouteBuilder MapDevLoginEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev-login", (DevLoginRequest request, IWebHostEnvironment env, HttpResponse response) =>
        {
            if (env.IsProduction())
                return Results.Forbid();

            var opts = new CookieOptions
            {
                HttpOnly = false,
                SameSite = SameSiteMode.Lax,
                Path     = "/"
            };

            response.Cookies.Append("X-Test-User-Id",     request.UserId,     opts);
            response.Cookies.Append("X-Test-Display-Name", request.DisplayName, opts);

            return Results.Ok(new { request.UserId, request.DisplayName });
        })
        .AllowAnonymous()
        .WithName("DevLogin")
        .ExcludeFromDescription();

        return app;
    }
}

public sealed record DevLoginRequest(string UserId, string DisplayName);
