using Serilog;
using Serilog.Events;
using PoFace.Api.Infrastructure.Configuration;

namespace PoFace.Api.Infrastructure.Logging;

public static class SerilogConfiguration
{
    public static WebApplicationBuilder AddPoFaceSerilog(this WebApplicationBuilder builder)
    {
        var appInsightsConnectionString = builder.Configuration.GetAppInsightsConnectionString();

        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("System", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithProperty("Environment", builder.Environment.EnvironmentName)
            .WriteTo.File(
                path: Path.Combine("logs", "poface-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Environment} {CorrelationId} {UserId} {SessionId} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Console(outputTemplate:
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
