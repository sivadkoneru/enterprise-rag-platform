namespace Rag.Core.Configuration;

public sealed class ApiOptions
{
    /// <summary>
    /// When set, the API requires a matching <c>X-API-Key</c> header on every request except
    /// <c>/health</c>, <c>/</c>, and the Swagger routes. When unset, the API accepts unauthenticated
    /// requests; this opt-in default keeps local development, the samples, and the existing README
    /// flow working without extra setup.
    /// </summary>
    public string? ApiKey { get; set; }
}
