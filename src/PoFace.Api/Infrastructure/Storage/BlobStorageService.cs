using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace PoFace.Api.Infrastructure.Storage;

public interface IBlobStorageService
{
    Task<string> UploadRoundImageAsync(
        string userId, string sessionId, int roundNumber, Stream imageStream,
        CancellationToken cancellationToken = default);

    Task<string> GetBlobUrlAsync(string blobPath);
}

public sealed class BlobStorageService : IBlobStorageService
{
    private const string ContainerName = "poface-captures";
    private readonly BlobServiceClient _client;

    public BlobStorageService(BlobServiceClient client) => _client = client;

    public async Task<string> UploadRoundImageAsync(
        string userId, string sessionId, int roundNumber, Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        var container = _client.GetBlobContainerClient(ContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: cancellationToken);

        var blobName   = $"{userId}/{sessionId}/round-{roundNumber}.jpg";
        var blobClient = container.GetBlobClient(blobName);

        await blobClient.UploadAsync(imageStream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "image/jpeg" }
        }, cancellationToken);

        return blobClient.Uri.ToString();
    }

    public Task<string> GetBlobUrlAsync(string blobPath)
    {
        var container = _client.GetBlobContainerClient(ContainerName);
        var blobClient = container.GetBlobClient(blobPath);
        return Task.FromResult(blobClient.Uri.ToString());
    }
}
