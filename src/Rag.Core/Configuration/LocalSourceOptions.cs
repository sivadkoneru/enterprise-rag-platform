namespace Rag.Core.Configuration;

public sealed class LocalSourceOptions
{
    /// <summary>
    /// Directories that local <c>file</c> sources may read from. An empty list means unrestricted,
    /// which is the default for library and CLI use where callers already run as the invoking user.
    /// Hosts that accept source paths from untrusted callers should set at least one root.
    /// </summary>
    public IList<string> AllowedRoots { get; } = [];
}
