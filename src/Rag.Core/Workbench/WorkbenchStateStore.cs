using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;

namespace Rag.Core.Workbench;

public sealed class InMemoryWorkbenchStateStore : IWorkbenchStateStore
{
    private readonly ConcurrentDictionary<string, string> _records = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _records[$"{collection}/{id}"] = JsonSerializer.Serialize(value, Json);
        return Task.CompletedTask;
    }
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_records.TryGetValue($"{collection}/{id}", out var value) ? JsonSerializer.Deserialize<T>(value, Json) : default);
    }
    public Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<T> values = _records.Where(pair => pair.Key.StartsWith($"{collection}/", StringComparison.Ordinal)).Select(pair => JsonSerializer.Deserialize<T>(pair.Value, Json)).OfType<T>().ToArray();
        return Task.FromResult(values);
    }
}

public sealed class FileWorkbenchStateStore(IOptions<DocumentStoreOptions> options) : IWorkbenchStateStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private string DirectoryPath(string collection) => Path.Combine(options.Value.LocalPath, "workbench", Validate(collection));
    private string RecordPath(string collection, string id) => Path.Combine(DirectoryPath(collection), $"{Validate(id)}.json");
    private static string Validate(string value) => value.Length > 0 && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_') ? value : throw new ArgumentException("Invalid catalog identifier.");
    public async Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DirectoryPath(collection));
        var path = RecordPath(collection, id);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(value, Json), cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
    public async Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default)
    {
        var path = RecordPath(collection, id);
        return File.Exists(path) ? JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), Json) : default;
    }
    public async Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default)
    {
        var directory = DirectoryPath(collection);
        var values = new List<T>();
        if (!Directory.Exists(directory))
        {
            return values;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.json"))
        {
            var value = JsonSerializer.Deserialize<T>(await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false), Json);
            if (value is not null)
            {
                values.Add(value);
            }
        }
        return values;
    }
}

public sealed class InMemoryWorkbenchJobStateStore : IWorkbenchJobStateStore
{
    private readonly InMemoryWorkbenchStateStore _inner = new();
    public Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default) => _inner.SaveAsync(collection, id, value, cancellationToken);
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) => _inner.GetAsync<T>(collection, id, cancellationToken);
    public Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default) => _inner.ListAsync<T>(collection, cancellationToken);
}

public sealed class WorkbenchCompositeStateStore(IWorkbenchStateStore catalog, IWorkbenchJobStateStore jobs) : IWorkbenchStateStore
{
    private IWorkbenchStateStore For(string collection) => collection == "jobs" ? jobs : catalog;
    public Task SaveAsync<T>(string collection, string id, T value, CancellationToken cancellationToken = default) => For(collection).SaveAsync(collection, id, value, cancellationToken);
    public Task<T?> GetAsync<T>(string collection, string id, CancellationToken cancellationToken = default) => For(collection).GetAsync<T>(collection, id, cancellationToken);
    public Task<IReadOnlyList<T>> ListAsync<T>(string collection, CancellationToken cancellationToken = default) => For(collection).ListAsync<T>(collection, cancellationToken);
}
