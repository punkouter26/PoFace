using System.Security.Claims;
using Serilog.Context;

namespace PoFace.Api.Infrastructure.Telemetry;

public sealed class RequestContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;

    public RequestContextMiddleware(RequestDelegate next, IWebHostEnvironment environment)
    {
        _next = next;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                     ?? context.User.FindFirstValue("sub")
                     ?? "anonymous";
        var sessionId = GetSessionId(context) ?? "n/a";

        using (LogContext.PushProperty("UserId", userId))
        using (LogContext.PushProperty("SessionId", sessionId))
        using (LogContext.PushProperty("Environment", _environment.EnvironmentName))
        {
            await _next(context);
        }
    }

    private static string? GetSessionId(HttpContext context)
    {
        if (context.Request.RouteValues.TryGetValue("sessionId", out var routeValue))
            return Convert.ToString(routeValue);

        var segments = context.Request.Path.Value?
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments is null || segments.Length < 2)
            return null;

        for (var index = 0; index < segments.Length - 1; index++)
        {
            if (segments[index].Equals("sessions", StringComparison.OrdinalIgnoreCase) ||
                segments[index].Equals("recap", StringComparison.OrdinalIgnoreCase))
                return segments[index + 1];
        }

        return null;
    }
}