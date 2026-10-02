using System.Globalization;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.DependencyInjection;
using Rag.Core.Workbench;
using Rag.Providers.Aws;
using Rag.Providers.AzureBlob;
using Xunit;

namespace Rag.Core.Tests;

/// <summary>
/// Guards `.env.example` against advertising settings that nothing reads. Each documented key is
/// probed through <c>AddRagPlatform</c> with a value distinct from the default; if the bound
/// options are unchanged, the key is dead configuration.
/// </summary>
public sealed class EnvExampleBindingTests
{
    /// <summary>Keys consumed by the host or a provider SDK rather than by AddRagPlatform.</summary>
    private static readonly Dictionary<string, string> Informational = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ASPNETCORE_ENVIRONMENT"] = "read by the ASP.NET Core host",
        ["ASPNETCORE_URLS"] = "read by the ASP.NET Core host",
        ["AWS_ACCESS_KEY_ID"] = "read by the AWS SDK credential chain",
        ["AWS_SECRET_ACCESS_KEY"] = "read by the AWS SDK credential chain"
    };

    [Fact]
    public void EveryDocumentedKeyIsBoundOrExplicitlyInformational()
    {
        var baseline = Snapshot(new Dictionary<string, string?>());

        var dead = ReadEnvExample()
            .Where(entry => !Informational.ContainsKey(entry.Key))
            .Where(entry => Snapshot(new Dictionary<string, string?> { [entry.Key] = Probe(entry.Value) }) == baseline)
            .Select(entry => entry.Key)
            .ToArray();

        dead.Should().BeEmpty(
            "every key in .env.example must change platform options; add it to AddRagPlatform, " +
            "list it as informational, or delete it from .env.example");
    }

    [Fact]
    public void InformationalKeysAreStillDocumented()
    {
        var documented = ReadEnvExample().Select(entry => entry.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Informational.Keys.Where(key => !documented.Contains(key))
            .Should()
            .BeEmpty("the informational list should not outlive the keys it excuses");
    }

    [Fact]
    public void DocumentedProviderSelectorsCoverTheSupportedValues()
    {
        var documented = ReadEnvExample().ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

        documented.Should().ContainKeys(
            // Provider selectors.
            "LLM_PROVIDER",
            "DOC_STORE",
            "VECTOR_STORE",
            "CHUNKING_STRATEGY",
            "JOB_STORE",
            // Grounding, async ingestion, and the local-source trust boundary.
            "LLM_SYSTEM_PROMPT",
            "INGESTION_MAX_PARALLELISM",
            "LOCAL_SOURCE_ALLOWED_ROOTS",
            "S3_ENDPOINT",
            "AZURE_BLOB_CONNECTION_STRING");
        documented["LLM_PROVIDER"].Should().BeOneOf("deterministic", "openai", "azure-openai");
        documented["DOC_STORE"].Should().BeOneOf("memory", "file", "mongo", "cosmos");
        documented["VECTOR_STORE"].Should().BeOneOf("memory", "elasticsearch");
        documented["JOB_STORE"].Should().BeOneOf("memory", "mongo");
    }

    private static string Snapshot(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        // S3Options and AzureBlobOptions now bind inside the opt-in Rag.Providers.Aws /
        // Rag.Providers.AzureBlob packages rather than AddRagPlatform itself, so both must be called
        // here for the S3_*/AZURE_BLOB_* keys documented in .env.example to bind at all.
        using var services = new ServiceCollection()
            .AddRagPlatform(configuration)
            .AddRagWorkbench(configuration)
            .AddRagAwsS3(configuration)
            .AddRagAzureBlob(configuration)
            .BuildServiceProvider();

        return JsonSerializer.Serialize(new
        {
            Rag = services.GetRequiredService<IOptions<RagOptions>>().Value,
            Query = services.GetRequiredService<IOptions<WorkbenchQueryOptions>>().Value,
            Reranker = services.GetRequiredService<IOptions<RerankerOptions>>().Value,
            Chunking = services.GetRequiredService<IOptions<ChunkingOptions>>().Value,
            Llm = services.GetRequiredService<IOptions<LlmOptions>>().Value,
            Ingestion = services.GetRequiredService<IOptions<IngestionOptions>>().Value,
            JobStore = services.GetRequiredService<IOptions<JobStoreOptions>>().Value,
            S3 = services.GetRequiredService<IOptions<S3Options>>().Value,
            AzureBlob = services.GetRequiredService<IOptions<AzureBlobOptions>>().Value,
            DocumentStore = services.GetRequiredService<IOptions<DocumentStoreOptions>>().Value,
            VectorStore = services.GetRequiredService<IOptions<VectorStoreOptions>>().Value,
            LocalSource = services.GetRequiredService<IOptions<LocalSourceOptions>>().Value,
            CloudSource = services.GetRequiredService<IOptions<CloudSourceOptions>>().Value,
            Api = services.GetRequiredService<IOptions<ApiOptions>>().Value
        });
    }

    /// <summary>Returns a value of the same shape as the example but distinct from any default.</summary>
    private static string Probe(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "probe-value";
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
        {
            return (integer + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return (number + 1).ToString(CultureInfo.InvariantCulture);
        }

        if (bool.TryParse(value, out var flag))
        {
            return (!flag).ToString();
        }

        return $"{value}-probe";
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ReadEnvExample()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../.env.example"));
        File.Exists(path).Should().BeTrue($"the documented environment template should exist at {path}");

        return File.ReadAllLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0].Length > 0)
            .Select(parts => new KeyValuePair<string, string>(parts[0].Trim(), parts[1].Trim()))
            .ToArray();
    }
}
