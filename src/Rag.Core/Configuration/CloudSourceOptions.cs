namespace Rag.Core.Configuration;

public sealed class CloudSourceOptions
{
    /// <summary>
    /// Cloud source URI prefixes that may be ingested, for example "s3://corp-docs/policies".
    /// An empty list means unrestricted, matching <see cref="LocalSourceOptions.AllowedRoots"/>.
    /// Hosts that accept source URIs from untrusted callers should set at least one prefix.
    /// </summary>
    public IList<string> AllowedPrefixes { get; } = [];
}
