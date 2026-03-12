using Serilog;
using Serilog.Events;
using PoFace.Api.Infrastructure.Configuration;

namespace PoFace.Api.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder AddPoFaceSerilog(this WebApplicationBuilder builder)
    {
        var appInsightsConnectionString = builder.Configuration.GetAppInsightsConnectionString();
        const string outputTemplate =
            "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Environment} {CorrelationId} {UserId} {SessionId} {Message:lj}{NewLine}{Exception}";

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName);

        // On Azure App Service with WEBSITE_RUN_FROM_PACKAGE the working dir is
        // a read-only ZIP mount.  Use WEBSITE_CONTENTSHARE's writable log path
        // (/home/LogFiles) when available, or fall back to /tmp.
        var logDir = builder.Environment.IsDevelopment()
            ? "logs"
            : (Environment.GetEnvironmentVariable("HOME") is { } home
                ? Path.Combine(home, "LogFiles", "Application")
                : Path.GetTempPath());

        if (!builder.Environment.IsDevelopment())
            Directory.CreateDirectory(logDir);

        loggerConfig = builder.Environment.IsDevelopment()
            ? loggerConfig.WriteTo.File(
                path: Path.Combine(logDir, $"poface-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Environment.ProcessId}.log"),
                retainedFileCountLimit: 20,
                outputTemplate: outputTemplate)
            : loggerConfig.WriteTo.File(
                path: Path.Combine(logDir, "poface-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: outputTemplate);

        loggerConfig = loggerConfig.WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] {Environment} {CorrelationId} {UserId} {SessionId} {Message:lj}{NewLine}{Exception}");

        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            loggerConfig.WriteTo.ApplicationInsights(
                appInsightsConnectionString,
                TelemetryConverter.Traces);
        }

        Log.Logger = loggerConfig.CreateLogger();
        builder.Host.UseSerilog();
        return builder;
    }
}
