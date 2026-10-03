using FluentAssertions;
using Microsoft.Extensions.Options;
using Rag.Core.Configuration;
using Rag.Core.Models;
using Rag.Core.Sources;
using Xunit;

namespace Rag.Core.Tests;

public sealed class CloudSourceGuardTests
{
    [Fact]
    public async Task EmptyAllowedPrefixesAllowsAnyUri()
    {
        var source = new FakeCloudSource(AllowedPrefixes());

        var items = await ReadAllAsync(source, "s3://corp-docs/policies");

        items.Select(item => item.FileName).Should().Equal("a.txt");
    }

    [Fact]
    public async Task MatchingPrefixAllowsTheUri()
    {
        var source = new FakeCloudSource(AllowedPrefixes("s3://corp-docs"));

        var items = await ReadAllAsync(source, "s3://corp-docs/policies");

        items.Select(item => item.FileName).Should().Equal("a.txt");
    }

    [Fact]
    public async Task NonMatchingUriIsRejected()
    {
        var source = new FakeCloudSource(AllowedPrefixes("s3://corp-docs"));

        var act = () => ReadAllAsync(source, "s3://other-bucket/policies");

        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task SiblingContainerSharingAPrefixIsNotTreatedAsInside()
    {
        var source = new FakeCloudSource(AllowedPrefixes("s3://corp-docs"));

        var act = () => ReadAllAsync(source, "s3://corp-docs-public/x");

        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task PrefixMatchingIsCaseInsensitive()
    {
        var source = new FakeCloudSource(AllowedPrefixes("S3://CORP-DOCS"));

        var items = await ReadAllAsync(source, "s3://corp-docs/policies");

        items.Select(item => item.FileName).Should().Equal("a.txt");
    }

    [Fact]
    public async Task ObjectPrefixMatchingIsCaseSensitive()
    {
        var source = new FakeCloudSource(AllowedPrefixes("s3://corp-docs/Private"));
        var act = () => ReadAllAsync(source, "s3://corp-docs/private");
        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    [Fact]
    public async Task EveryReturnedObjectMustRemainWithinTheAllowedPrefix()
    {
        var source = new FakeCloudSource(AllowedPrefixes("s3://corp-docs/policies"));
        // This fake deliberately returns a.txt outside the requested prefix.
        var act = () => ReadAllAsync(source, "s3://corp-docs/policies");
        await act.Should().ThrowAsync<SourcePathNotAllowedException>();
    }

    private static IOptions<CloudSourceOptions> AllowedPrefixes(params string[] prefixes)
    {
        var options = new CloudSourceOptions();
        foreach (var prefix in prefixes)
        {
            options.AllowedPrefixes.Add(prefix);
        }

        return Options.Create(options);
    }

    private static async Task<IReadOnlyList<SourceItem>> ReadAllAsync(FakeCloudSource source, string uri)
    {
        var items = new List<SourceItem>();
        await foreach (var item in source.EnumerateAsync(uri))
        {
            items.Add(item);
        }

        return items;
    }

    private sealed class FakeCloudSource(IOptions<CloudSourceOptions> options) : CloudDocumentSource(options)
    {
        public override string Scheme => "s3";

        protected override Task<IReadOnlyList<string>> ListKeysAsync(string container, string prefix, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> keys = ["a.txt"];
            return Task.FromResult(keys);
        }

        protected override Task DownloadAsync(string container, string key, string localPath, CancellationToken cancellationToken)
        {
            File.WriteAllBytes(localPath, "content"u8.ToArray());
            return Task.CompletedTask;
        }

        protected override IEnumerable<KeyValuePair<string, string>> DescribeItem(string container, string key)
        {
            yield return new KeyValuePair<string, string>("bucket", container);
            yield return new KeyValuePair<string, string>("key", key);
        }
    }
}
