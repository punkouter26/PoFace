using System.Reflection;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using MediatR;
using PoFace.Api.Infrastructure.Configuration;
using PoFace.Api.Features.Scoring;

namespace PoFace.Api.Features.Diagnostics;

public sealed record DiagnosticsQuery : IRequest<DiagnosticsReport>;

public sealed record ServiceStatus(string Status, string? Message = null);

public sealed record DiagnosticsServices(
    ServiceStatus FaceApi,
    ServiceStatus BlobStorage,
    ServiceStatus TableStorage);

public sealed record DiagnosticsConfig(
    string FaceApiKeyMasked,
    string BlobAccountName,
    string AppInsightsConnectionMasked);

public sealed record DiagnosticsReport(
    string Version,
    string Region,
    DateTimeOffset Timestamp,
    DiagnosticsServices Services,
    DiagnosticsConfig Config);

public sealed class DiagnosticsQueryHandler : IRequestHandler<DiagnosticsQuery, DiagnosticsReport>
{
    private static readonly byte[] TinyJpeg =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46,
        0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01,
        0x00, 0x01, 0x00, 0x00, 0xFF, 0xD9
    ];

    private readonly BlobServiceClient _blobClient;
    private readonly TableServiceClient _tableClient;
    private readonly IFaceAnalysisService _faceAnalysis;
    private readonly IConfiguration _configuration;
    private readonly ConfigMaskingService _masking;
    private readonly IHostEnvironment _environment;

    public DiagnosticsQueryHandler(
        BlobServiceClient blobClient,
        TableServiceClient tableClient,
        IFaceAnalysisService faceAnalysis,
        IConfiguration configuration,
        ConfigMaskingService masking,
        IHostEnvironment environment)
    {
        _blobClient = blobClient;
        _tableClient = tableClient;
        _faceAnalysis = faceAnalysis;
        _configuration = configuration;
        _masking = masking;
        _environment = environment;
    }

    public async Task<DiagnosticsReport> Handle(DiagnosticsQuery request, CancellationToken cancellationToken)
    {
        var blob = await ProbeAsync(async () =>
        {
            await _blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        });

        var table = await ProbeAsync(async () =>
        {
            await _tableClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        });

        var face = await ProbeFaceAsync(cancellationToken);

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0";
        var region = Environment.GetEnvironmentVariable("WEBSITE_REGION") ?? "local";

        var faceKey = _configuration["AzureFace:ApiKey"] ?? string.Empty;
        var appInsights = _configuration.GetAppInsightsConnectionString() ?? string.Empty;
        var accountName = _configuration["AzureStorage:AccountName"] ?? "unknown";

        return new DiagnosticsReport(
            Version: version,
            Region: region,
            Timestamp: DateTimeOffset.UtcNow,
            Services: new DiagnosticsServices(face, blob, table),
            Config: new DiagnosticsConfig(
                FaceApiKeyMasked: _masking.Mask(faceKey),
                BlobAccountName: accountName,
                AppInsightsConnectionMasked: _masking.Mask(appInsights)));
    }

    private static async Task<ServiceStatus> ProbeAsync(Func<Task> action)
    {
        try
        {
            await action();
            return new ServiceStatus("OK");
        }
        catch (Exception ex)
        {
            return new ServiceStatus("ERROR", ex.Message);
        }
    }

    private async Task<ServiceStatus> ProbeFaceAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsProduction() && !_environment.IsStaging())
        {
            var faceEndpoint = _configuration["AzureFace:Endpoint"] ?? string.Empty;
            if (string.IsNullOrWhiteSpace(faceEndpoint) ||
                faceEndpoint.Contains("placeholder", StringComparison.OrdinalIgnoreCase))
            {
                return new ServiceStatus("OK", "Using local stub face analysis.");
            }

            return new ServiceStatus("OK", "Active Face API probe skipped in Development to avoid noisy 400 responses.");
        }

        return await ProbeAsync(async () =>
        {
            _ = await _faceAnalysis.AnalyzeFrameAsync(TinyJpeg, "Happiness", cancellationToken);
        });
    }
}
