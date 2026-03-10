using Azure;
using Azure.Data.Tables;

namespace PoFace.Api.Infrastructure.Storage;

public interface ITableStorageService
{
    Task UpsertEntityAsync<T>(string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();

    Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new();
}

public sealed class TableStorageService : ITableStorageService
{
    private readonly TableServiceClient _client;

    public TableStorageService(TableServiceClient client) => _client = client;

    public async Task UpsertEntityAsync<T>(
        string tableName, T entity, CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        var table = _client.GetTableClient(tableName);
        await table.CreateIfNotExistsAsync(cancellationToken);
        await table.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<T?> GetEntityAsync<T>(
        string tableName, string partitionKey, string rowKey,
        CancellationToken cancellationToken = default)
        where T : class, ITableEntity, new()
    {
        var table = _client.GetTableClient(tableName);
        await table.CreateIfNotExistsAsync(cancellationToken);

        try
        {
            var response = await table.GetEntityAsync<T>(partitionKey, rowKey, cancellationToken: cancellationToken);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }
}
