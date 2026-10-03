using System.Net.Http.Json;
using System.Text.Json;
using Rag.Core.Workbench;

namespace Rag.LiveEvals;

/// <summary>Calls the existing workbench; it contains no retrieval or generation pipeline.</summary>
public sealed class LiveApi : IDisposable
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly HttpClient _client;
    public LiveApi(string destination, string key)
    {
        var uri = DirectUri(destination);
        _client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(uri.ToString().TrimEnd('/') + "/api/v1/"), Timeout = TimeSpan.FromMinutes(5) };
        _client.DefaultRequestHeaders.Add("X-API-Key", key);
    }
    public static Uri DirectUri(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || uri.UserInfo.Length + uri.Query.Length + uri.Fragment.Length > 0)
        {
            throw new ArgumentException("Use a direct HTTP(S) URL without credentials, query or fragment.");
        }

        return uri;
    }
    public async Task<T> GetAsync<T>(string path, CancellationToken token) => await _client.GetFromJsonAsync<T>(path, Json, token).ConfigureAwait(false) ?? throw new InvalidDataException("API response missing.");
    public async Task<DetailedRun> QueryAsync(DetailedQueryRequest input, CancellationToken token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "queries") { Content = JsonContent.Create(input) };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false));
        string? kind = null;
        DetailedRun? result = null;
        while (await reader.ReadLineAsync(token).ConfigureAwait(false) is { } line)
        {
            if (line.Length > 2_000_000)
            {
                throw new InvalidDataException("Oversized API event.");
            }

            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                kind = line[7..];
            }

            if (!line.StartsWith("data: ", StringComparison.Ordinal))
            {
                continue;
            }

            if (kind == "error")
            {
                throw new InvalidOperationException("Workbench query failed; inspect private diagnostics.");
            }

            if (kind == "result")
            {
                result = JsonSerializer.Deserialize<DetailedRun>(line[6..], Json);
            }
        }
        return result ?? throw new InvalidDataException("Query ended without a result.");
    }
    public void Dispose() => _client.Dispose();
}
