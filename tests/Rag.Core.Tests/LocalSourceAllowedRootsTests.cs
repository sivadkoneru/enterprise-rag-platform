using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Sources;
using Xunit;

namespace Rag.Core.Tests;

public sealed class LocalSourceAllowedRootsTests : IDisposable
{
    private readonly string _allowedRoot = Path.Combine(Path.GetTempPath(), $"rag-allowed-{Guid.NewGuid():N}");
    private readonly string _forbiddenRoot = Path.Combine(Path.GetTempPath(), $"rag-forbidden-{Guid.NewGuid():N}");

    public LocalSourceAllowedRootsTests()
    {
        Directory.CreateDirectory(_allowedRoot);
        Directory.CreateDirectory(_forbiddenRoot);
        File.WriteAllText(Path.Combine(_allowedRoot, "allowed.txt"), "Refunds are available within thirty days.");
        File.WriteAllText(Path.Combine(_forbiddenRoot, "secret.txt"), "credentials");
    }

    [Fact]
    public async Task EmptyAllowedRootsKeepsUnrestrictedLibraryAndCliBehavior()
    {
        var source = new LocalDirectorySource(Options.Create(new LocalSourceOptions()));

        var items = await ReadAllAsync(source, _forbiddenRoot);

        items.Select(item => item.FileName).Should().Equal("secret.txt");
    }

    [Fact]
    public async Task PathsOutsideTheAllowedRootsAreRejected()
    {
        var source = new LocalDirectorySource(Options.Create(Restricted()));

        var act = () => ReadAllAsync(source, _forbiddenRoot);

        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task TraversalOutOfAnAllowedRootIsRejected()
    {
        var source = new LocalDirectorySource(Options.Create(Restricted()));
        var traversal = Path.Combine(_allowedRoot, "..", Path.GetFileName(_forbiddenRoot));

        var act = () => ReadAllAsync(source, traversal);

        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task PathsInsideAnAllowedRootAreEnumerated()
    {
        var source = new LocalDirectorySource(Options.Create(Restricted()));

        var items = await ReadAllAsync(source, _allowedRoot);

        items.Select(item => item.FileName).Should().Equal("allowed.txt");
    }

    [Fact]
    public async Task SiblingDirectoriesSharingARootPrefixAreNotTreatedAsInside()
    {
        var sibling = $"{_allowedRoot}-extra";
        Directory.CreateDirectory(sibling);
        File.WriteAllText(Path.Combine(sibling, "sneaky.txt"), "nope");
        try
        {
            var source = new LocalDirectorySource(Options.Create(Restricted()));

            var act = () => ReadAllAsync(source, sibling);

            await act.Should().ThrowAsync<SourcePathNotAllowedException>();
        }
        finally
        {
            Directory.Delete(sibling, recursive: true);
        }
    }

    [Fact]
    public async Task SymlinkedFilesResolvingOutsideAnAllowedRootAreRejected()
    {
        var link = Path.Combine(_allowedRoot, "linked.txt");
        try
        {
            File.CreateSymbolicLink(link, Path.Combine(_forbiddenRoot, "secret.txt"));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return; // Symlink creation is not permitted on this machine.
        }

        var source = new LocalDirectorySource(Options.Create(Restricted()));

        var act = () => ReadAllAsync(source, _allowedRoot);

        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task AncestorDirectorySymlinksCannotEscapeAllowedRoots()
    {
        var link = Path.Combine(_allowedRoot, "linked-directory");
        Directory.CreateSymbolicLink(link, _forbiddenRoot);
        var source = new LocalDirectorySource(Options.Create(Restricted()));
        var act = () => ReadAllAsync(source, Path.Combine(link, "secret.txt"));
        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task SchemaSymlinksOutsideAllowedRootAreNotOpened()
    {
        File.WriteAllText(Path.Combine(_forbiddenRoot, "schema.json"), "secret");
        File.CreateSymbolicLink(Path.Combine(_allowedRoot, "allowed.txt.schema.json"), Path.Combine(_forbiddenRoot, "schema.json"));
        var items = await ReadAllAsync(new LocalDirectorySource(Options.Create(Restricted())), Path.Combine(_allowedRoot, "allowed.txt"));
        items.Single().Attributes.Should().NotContainKey("schemaPath");
        items.Single().Attributes.Should().ContainKey("schemaResolved");
    }

    public void Dispose()
    {
        Directory.Delete(_allowedRoot, recursive: true);
        Directory.Delete(_forbiddenRoot, recursive: true);
    }

    private LocalSourceOptions Restricted()
    {
        var options = new LocalSourceOptions();
        options.AllowedRoots.Add(_allowedRoot);
        return options;
    }

    private static async Task<IReadOnlyList<SourceItem>> ReadAllAsync(LocalDirectorySource source, string uri)
    {
        var items = new List<SourceItem>();
        await foreach (var item in source.EnumerateAsync(uri))
        {
            items.Add(item);
        }

        return items;
    }
}
