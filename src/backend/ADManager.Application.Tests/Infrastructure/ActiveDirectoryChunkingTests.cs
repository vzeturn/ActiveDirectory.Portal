using ADManager.Infrastructure.ActiveDirectory;
using Xunit;

namespace ADManager.Application.Tests.Infrastructure;

public sealed class ActiveDirectoryChunkingTests
{
    [Fact]
    public void ChunkIdentifiers_EmptyList_ReturnsNoChunks()
    {
        var chunks = ActiveDirectoryGatewayTestHooks.ChunkIdentifiers(new List<string>(), 900);
        Assert.Empty(chunks);
    }

    [Fact]
    public void ChunkIdentifiers_ExactlyBatchSize_ReturnsSingleChunk()
    {
        var identifiers = Enumerable.Range(0, 900).Select(i => $"dn{i}").ToList();
        var chunks = ActiveDirectoryGatewayTestHooks.ChunkIdentifiers(identifiers, 900);
        Assert.Single(chunks);
        Assert.Equal(900, chunks[0].Count);
    }

    [Fact]
    public void ChunkIdentifiers_OneOverBatchSize_ReturnsTwoChunksWithSecondHavingOne()
    {
        var identifiers = Enumerable.Range(0, 901).Select(i => $"dn{i}").ToList();
        var chunks = ActiveDirectoryGatewayTestHooks.ChunkIdentifiers(identifiers, 900);
        Assert.Equal(2, chunks.Count);
        Assert.Equal(900, chunks[0].Count);
        Assert.Single(chunks[1]);
    }

    [Fact]
    public void ChunkIdentifiers_PreservesOriginalOrderAcrossChunks()
    {
        var identifiers = Enumerable.Range(0, 1801).Select(i => $"dn{i}").ToList();
        var chunks = ActiveDirectoryGatewayTestHooks.ChunkIdentifiers(identifiers, 900);
        var flattened = chunks.SelectMany(c => c).ToList();
        Assert.Equal(identifiers, flattened);
    }
}
