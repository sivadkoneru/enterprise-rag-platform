using Xunit;

namespace Rag.Integration.Tests;

/// <summary>
/// Serializes the cloud source suites. They share the `rag-ingest` temporary directory, so running
/// them concurrently would make temp-file assertions observe each other's in-flight downloads. It
/// also keeps peak container count down on developer machines.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class CloudSourceCollection
{
    public const string Name = "cloud sources";
}
