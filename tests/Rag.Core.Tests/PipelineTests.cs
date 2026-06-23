using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rag.Core.Abstractions;
using Rag.Core.DependencyInjection;
using Rag.Core.Models;
using Xunit;

namespace Rag.Core.Tests;

public sealed class PipelineTests
{
    [Fact]
    public async Task IngestThenQueryReturnsCitation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        await File.WriteAllTextAsync(path, "Refunds are available within thirty days with a receipt.");
        var services = new ServiceCollection()
            .AddRagPlatform(new ConfigurationBuilder().Build())
            .BuildServiceProvider();

        await services.GetRequiredService<IIngestionPipeline>().IngestAsync(new IngestionRequest(path));
        var answer = await services.GetRequiredService<IQueryPipeline>().QueryAsync(new QueryRequest("What is the refund policy?"));

        answer.Citations.Should().NotBeEmpty();
    }
}
