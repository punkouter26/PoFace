using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Storage.Blobs;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Vision.V1;
using Microsoft.Identity.Web;
using PoFace.Api.Features.Auth;
using PoFace.Api.Features.Diagnostics;
using PoFace.Api.Features.GameSession;
using PoFace.Api.Features.Leaderboard;
using PoFace.Api.Features.Recap;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Auth;
using PoFace.Api.Infrastructure.Configuration;
using PoFace.Api.Infrastructure.KeyVault;
using PoFace.Api.Infrastructure.Logging;
using PoFace.Api.Infrastructure.Storage;
using PoFace.Api.Infrastructure.Telemetry;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// ── Key Vault ─────────────────────────────────────────────────────────────────
builder.Configuration.AddPoFaceKeyVault(builder.Environment);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.AddPoFaceSerilog();

// ── OpenAPI ───────────────────────────────────────────────────────────────────
builder.Services.AddOpenApi();

// ── Google Cloud Vision API ─────────────────────────────────────────────────────────
var visionCredentialJson = builder.Configuration["GoogleVision:CredentialJson"] ?? string.Empty;
if (!string.IsNullOrWhiteSpace(visionCredentialJson))
{
    builder.Services.AddSingleton(_ =>
    {
        using var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(visionCredentialJson));
        var googleCredential = ServiceAccountCredential.FromServiceAccountData(stream)
            .ToGoogleCredential()
            .CreateScoped(ImageAnnotatorClient.DefaultScopes);
        return new ImageAnnotatorClientBuilder { GoogleCredential = googleCredential }.Build();
    });
    builder.Services.AddScoped<IFaceAnalysisService, GoogleVisionFaceAnalysisService>();
}
else
{
    builder.Services.AddScoped<IFaceAnalysisService, StubFaceAnalysisService>();
}

// ── Azure Storage ─────────────────────────────────────────────────────────────
var storageConnectionString = builder.Configuration.GetRequiredStorageConnectionString(builder.Environment);

builder.Services.AddSingleton(_ =>
    new BlobServiceClient(storageConnectionString));

builder.Services.AddSingleton(_ =>
    new TableServiceClient(storageConnectionString));

builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<ITableStorageService, TableStorageService>();
builder.Services.AddSingleton<ILeaderboardTableRepository, LeaderboardTableRepository>();
builder.Services.AddSingleton<IBlobImageRepository, BlobImageRepository>();
builder.Services.AddSingleton<IGameSessionLookupService, GameSessionLookupService>();
builder.Services.AddSingleton<ConfigMaskingService>();

// ── MediatR ───────────────────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<Program>());

// ── OTel + Azure Monitor ──────────────────────────────────────────────────────
// UseAzureMonitor wires traces, metrics, and logs to App Insights.
// AddPoFaceMetrics registers the custom PoFace meter on the same builder.
var openTelemetry = builder.Services.AddOpenTelemetry();

var appInsightsCs = builder.Configuration.GetAppInsightsConnectionString();
if (!string.IsNullOrWhiteSpace(appInsightsCs))
    openTelemetry.UseAzureMonitor(o => o.ConnectionString = appInsightsCs);

openTelemetry.AddPoFaceMetrics();

// ── CORS ──────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5231", "https://localhost:7224"];
builder.Services.AddCors(options =>
    options.AddPolicy("PoFaceClient", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

// ── Authentication / Authorization ────────────────────────────────────────────
// Use real Microsoft Entra ID auth when AzureAd is configured (any environment);
// fall back to header-based PoTestAuth bypass in local dev without credentials.
var azureAdTenantId = builder.Configuration["AzureAd:TenantId"];
if (!string.IsNullOrWhiteSpace(azureAdTenantId))
{
    // T017 — Microsoft Entra ID / MSAL token validation
    builder.Services.AddMicrosoftIdentityWebApiAuthentication(builder.Configuration);
}
else if (!builder.Environment.IsProduction() && !builder.Environment.IsStaging())
{
    // T016 — Dev/Test: header-based bypass — only when AzureAd is NOT configured
    builder.Services.AddPoTestAuth();
}
else
{
    // Production without AzureAd config is a fatal misconfiguration
    throw new InvalidOperationException("AzureAd:TenantId must be configured in Production.");
}

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// ── Problem Details ───────────────────────────────────────────────────────────
builder.Services.AddProblemDetails();

var app = builder.Build();

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseMiddleware<CorrelationIdMiddleware>();    // T014 — must be first
app.UseExceptionHandler();

if (!app.Environment.IsProduction())
    app.UseDeveloperExceptionPage();

app.UseBlazorFrameworkFiles();

app.UseSerilogRequestLogging();
app.UseCors("PoFaceClient");

app.UseAuthentication();
app.UseMiddleware<RequestContextMiddleware>();
app.UseAuthorization();

app.MapOpenApi("/openapi/{documentName}.json")
    .AllowAnonymous();
app.MapScalarApiReference("/scalar")
    .AllowAnonymous();

// ── Feature Endpoints ────────────────────────────────────────────────────────
app.MapAuthEndpoints();
app.MapGameSessionEndpoints();
app.MapScoringEndpoints();
app.MapLeaderboardEndpoints();
app.MapRecapEndpoints();
app.MapDiagnosticsEndpoints();

app.MapGet("/health", HandleHealthAsync)
    .AllowAnonymous();
app.MapGet("/api/health", HandleHealthAsync)
    .AllowAnonymous();

static async Task<IResult> HandleHealthAsync(MediatR.ISender sender, CancellationToken cancellationToken)
{
    var report = await sender.Send(new DiagnosticsQuery(), cancellationToken);
    var services = new[]
    {
        report.Services.FaceApi.Status,
        report.Services.BlobStorage.Status,
        report.Services.TableStorage.Status
    };

    var healthy = services.All(static status => status.Equals("OK", StringComparison.OrdinalIgnoreCase));

    return Results.Json(
        new
        {
            status = healthy ? "ok" : "degraded",
            report.Version,
            report.Region,
            report.Timestamp,
            services = new
            {
                faceApi = report.Services.FaceApi,
                blobStorage = report.Services.BlobStorage,
                tableStorage = report.Services.TableStorage
            }
        },
        statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
}

// Unknown /api/* paths must return 404, not fall through to the SPA (OWASP A05).
app.MapGet("/api/{**slug}", () => Results.NotFound()).AllowAnonymous();

app.UseStaticFiles();

app.MapFallbackToFile("index.html")
    .AllowAnonymous();

app.Run();

// Make Program accessible to WebApplicationFactory in integration tests
public partial class Program { }
