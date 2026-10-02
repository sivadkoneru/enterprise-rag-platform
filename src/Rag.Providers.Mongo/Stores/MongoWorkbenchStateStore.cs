using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Rag.Core.Configuration;
using Rag.Core.Workbench;

namespace Rag.Providers.Mongo.Stores;

public sealed class MongoWorkbenchStateStore : IWorkbenchStateStore
{
    private readonly IMongoDatabase _database;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public MongoWorkbenchStateStore(IOptions<DocumentStoreOptions> options)
    {
        if (string.IsNullOrWhiteSpace(options.Value.ConnectionString))
        {
            throw new InvalidOperationException("Configure the MongoDB connection string for persistent workbench state.");
        }

        _database = new MongoClient(options.Value.ConnectionString).GetDatabase(options.Value.DatabaseName);
    }
    private IMongoCollection<BsonDocument> Collection(string collection) => _database.GetCollection<BsonDocument>($"workbench_{collection}");
    public async Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default)
    {
        var record = new BsonDocument { ["_id"] = id, ["payload"] = JsonSerializer.Serialize(value, Json) };
        await Collection(collection).ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("_id", id), record, new ReplaceOptions { IsUpsert = true }, cancellationToken).ConfigureAwait(false);
    }
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var record = await Collection(collection).Find(Builders<BsonDocument>.Filter.Eq("_id", id)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return record is null ? default : JsonSerializer.Deserialize<T>(record["payload"].AsString, Json);
    }
    public async Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        using var cursor = await Collection(collection).FindAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken).ConfigureAwait(false);
        while (await cursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
        {
            foreach (var record in cursor.Current)
            {
                var value = JsonSerializer.Deserialize<T>(record["payload"].AsString, Json);
                if (value is not null)
                {
                    result.Add(value);
                }
            }
        }
        return result;
    }
}

public sealed class MongoWorkbenchJobStateStore : IWorkbenchJobStateStore
{
    private readonly MongoWorkbenchStateStore _inner;
    public MongoWorkbenchJobStateStore(IOptions<JobStoreOptions> options)
    {
        _inner = new MongoWorkbenchStateStore(Options.Create(new DocumentStoreOptions { Provider = "mongo", ConnectionString = options.Value.ConnectionString, DatabaseName = options.Value.DatabaseName }));
    }
    public Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default) => _inner.SaveAsync(collection, id, value, cancellationToken);
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) => _inner.GetAsync<T>(collection, id, cancellationToken);
    public Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default) => _inner.ListAsync<T>(collection, cancellationToken);
}
