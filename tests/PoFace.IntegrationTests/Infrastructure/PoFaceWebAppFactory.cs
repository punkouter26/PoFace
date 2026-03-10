using Azure.AI.Vision.Face;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PoFace.Api.Features.Scoring;
using PoFace.Api.Infrastructure.Auth;
using PoFace.Api.Infrastructure.Storage;

namespace PoFace.IntegrationTests.Infrastructure;

/// <summary>
/// WebApplicationFactory for PoFace.Api integration tests.
/// Swaps Azure Storage clients for Azurite-backed instances and
/// registers the test-only PoTestAuth authentication scheme.
/// </summary>
public sealed class PoFaceWebAppFactory : WebApplicationFactory<Program>
{
    private readonly string _azuriteConnectionString;

    public PoFaceWebAppFactory(string azuriteConnectionString)
        => _azuriteConnectionString = azuriteConnectionString;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Remove real Azure Storage registrations
            RemoveService<BlobServiceClient>(services);
            RemoveService<TableServiceClient>(services);
            RemoveService<IBlobStorageService>(services);
            RemoveService<ITableStorageService>(services);

            // Replace with Azurite-backed instances
            services.AddSingleton(_ => new BlobServiceClient(_azuriteConnectionString));
            services.AddSingleton(_ => new TableServiceClient(_azuriteConnectionString));
            services.AddSingleton<IBlobStorageService, BlobStorageService>();
            services.AddSingleton<ITableStorageService, TableStorageService>();

            // Remove real FaceClient (not available in test environment).
            RemoveService<FaceClient>(services);
            RemoveService<IFaceAnalysisService>(services);
            services.AddScoped<IFaceAnalysisService, StubFaceAnalysisService>();

            // Program.cs already registers PoTestAuth in Testing environment.
            // Do not register it again here to avoid duplicate scheme errors.
        });
    }

    private static void RemoveService<T>(IServiceCollection services)
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(T));
        if (descriptor is not null)
            services.Remove(descriptor);
    }
}

/// <summary>
/// Test double for <see cref="IFaceAnalysisService"/> used in integration tests.
/// Returns a deterministic "face not detected" result so no real Face API calls are made.
/// </summary>
internal sealed class StubFaceAnalysisService : IFaceAnalysisService
{
    public Task<AnalysisResult> AnalyzeFrameAsync(
        byte[] imageBytes, string targetEmotion, CancellationToken cancellationToken = default)
        => Task.FromResult(new AnalysisResult
        {
            FaceDetected            = false,
            EmotionLabel            = targetEmotion,
            TargetEmotionConfidence = 0,
            HeadPoseYaw             = 0,
            HeadPosePitch           = 0,
            HeadPoseValid           = false,
            Score                   = 0
        });
}
