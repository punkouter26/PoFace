using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace PoFace.Api.Infrastructure.Auth;

/// <summary>
/// Test-only authentication scheme that authenticates a request if
/// X-Test-User-Id and X-Test-Display-Name headers are present.
/// MUST NOT be registered in Production environments.
/// </summary>
public static class PoTestAuthExtensions
{
    public const string SchemeName = "PoTestAuth";

    public static IServiceCollection AddPoTestAuth(this IServiceCollection services)
    {
        services.AddAuthentication(SchemeName)
            .AddScheme<AuthenticationSchemeOptions, PoTestAuthHandler>(SchemeName, _ => { });

        return services;
    }
}

internal sealed class PoTestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public PoTestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? userId;
        if (Request.Headers.TryGetValue("X-Test-User-Id", out var headerUserId) &&
            !string.IsNullOrWhiteSpace(headerUserId))
        {
            userId = headerUserId.ToString();
        }
        else
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var displayName = Request.Headers["X-Test-Display-Name"].FirstOrDefault()
                       ?? "Test User";

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Name, displayName),
            new Claim("sub", userId)
        };

        var identity  = new ClaimsIdentity(claims, PoTestAuthExtensions.SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket    = new AuthenticationTicket(principal, PoTestAuthExtensions.SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
