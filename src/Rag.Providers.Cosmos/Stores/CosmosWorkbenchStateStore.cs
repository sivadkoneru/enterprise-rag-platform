using System.Net;
using System.Text.Json;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Workbench;

namespace Rag.Providers.Cosmos.Stores;

public sealed class CosmosWorkbenchStateStore(IOptions<DocumentStoreOptions> options) : IWorkbenchStateStore, IDisposable
{
    private CosmosClient? _client;
    private Container? _container;
    private readonly SemaphoreSlim _initialize = new(1, 1);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private sealed record StateRecord(string id, string kind, string payload);
    private async Task<Container> GetContainerAsync(CancellationToken token)
    {
        if (_container is not null)
        {
            return _container;
        }

        await _initialize.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_container is not null)
            {
                return _container;
            }

            var config = options.Value;
            if (!string.IsNullOrWhiteSpace(config.ConnectionString))
            {
                _client = new CosmosClient(config.ConnectionString);
            }
            else if (!string.IsNullOrWhiteSpace(config.Endpoint) && !string.IsNullOrWhiteSpace(config.Key))
            {
                _client = new CosmosClient(config.Endpoint, config.Key);
            }
            else
            {
                throw new InvalidOperationException("Configure Cosmos credentials before using workbench persistence.");
            }

            var database = await _client.CreateDatabaseIfNotExistsAsync(config.DatabaseName, cancellationToken: token).ConfigureAwait(false);
            _container = (await database.Database.CreateContainerIfNotExistsAsync("workbench", "/kind", cancellationToken: token).ConfigureAwait(false)).Container;
            return _container;
        }
        finally { _initialize.Release(); }
    }
    public async Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken).ConfigureAwait(false);
        await container.UpsertItemAsync(new StateRecord(id, collection, JsonSerializer.Serialize(value, Json)), new PartitionKey(collection), cancellationToken: cancellationToken).ConfigureAwait(false);
    }
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken).ConfigureAwait(false);
        using var response = await container.ReadItemStreamAsync(id, new PartitionKey(collection), cancellationToken: cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return default;
        }

        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(response.Content, cancellationToken: cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json.RootElement.GetProperty("payload").GetString() ?? "null", Json);
    }
    public async Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync(cancellationToken).ConfigureAwait(false);
        using var iterator = container.GetItemQueryIterator<StateRecord>(new QueryDefinition("SELECT * FROM c WHERE c.kind = @kind").WithParameter("@kind", collection), requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(collection) });
        var result = new List<T>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            foreach (var record in page)
            {
                var value = JsonSerializer.Deserialize<T>(record.payload, Json);
                if (value is not null)
                {
                    result.Add(value);
                }
            }
        }
        return result;
    }
    public void Dispose() { _client?.Dispose(); _initialize.Dispose(); }
}
