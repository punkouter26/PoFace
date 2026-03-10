using Azure.Storage.Blobs;

namespace PoFace.Api.Features.Recap;

// ── Contract ─────────────────────────────────────────────────────────────────

public interface IBlobImageRepository
{
    /// <summary>
    /// Returns an ordered list of image URLs for all rounds in a session.
    /// If a blob does not exist, <see cref="PlaceholderImageUrl"/> is used instead.
    /// </summary>
    Task<IReadOnlyList<string>> GetRoundImageUrlsAsync(
        string userId,
        string sessionId,
        int    roundCount = 5,
        CancellationToken ct = default);
}

// ── Implementation ────────────────────────────────────────────────────────────

public sealed class BlobImageRepository : IBlobImageRepository
{
    /// <summary>Fallback URL used when a round image blob does not exist in Storage.</summary>
    public const string PlaceholderImageUrl = "/images/placeholder-round.jpg";

    private const string ContainerName = "poface-captures";

    private readonly BlobServiceClient _blobService;

    public BlobImageRepository(BlobServiceClient blobService)
        => _blobService = blobService;

    public async Task<IReadOnlyList<string>> GetRoundImageUrlsAsync(
        string userId,
        string sessionId,
        int    roundCount = 5,
        CancellationToken ct = default)
    {
        var container = _blobService.GetBlobContainerClient(ContainerName);
        var urls      = new List<string>(roundCount);

        for (int i = 1; i <= roundCount; i++)
        {
            var blobName   = $"{userId}/{sessionId}/round-{i}.jpg";
            var blobClient = container.GetBlobClient(blobName);
            var exists     = await blobClient.ExistsAsync(ct);

            urls.Add(exists.Value ? blobClient.Uri.ToString() : PlaceholderImageUrl);
        }

        return urls;
    }
}
