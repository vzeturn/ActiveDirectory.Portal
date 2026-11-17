using ADManager.Domain;
using ADManager.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Storage;

public sealed class SqliteSavedSearchRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly SqliteConnectionFactory _factory;

    public SqliteSavedSearchRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"ad-manager-test-{Guid.NewGuid()}.db");
        _factory = new SqliteConnectionFactory(Options.Create(new SqliteStorageSettings { DatabasePath = _dbPath }));
        new SqliteSchemaInitializer().EnsureCreated(_factory);
    }

    [Fact]
    public async Task CreateAsync_ThenListByOwnerAsync_ReturnsSavedSearch()
    {
        var repo = new SqliteSavedSearchRepository(_factory);
        var search = new SavedSearch(Guid.NewGuid().ToString(), "alice", "Locked accounts", "{\"lockedOnly\":true}", DateTimeOffset.UtcNow);

        await repo.CreateAsync(search);
        var results = await repo.ListByOwnerAsync("alice");

        Assert.Single(results);
        Assert.Equal("Locked accounts", results[0].Name);
    }

    [Fact]
    public async Task ListByOwnerAsync_DoesNotReturnOtherOwnersSearches()
    {
        var repo = new SqliteSavedSearchRepository(_factory);
        await repo.CreateAsync(new SavedSearch(Guid.NewGuid().ToString(), "alice", "A", "{}", DateTimeOffset.UtcNow));
        await repo.CreateAsync(new SavedSearch(Guid.NewGuid().ToString(), "bob", "B", "{}", DateTimeOffset.UtcNow));

        var results = await repo.ListByOwnerAsync("alice");

        Assert.Single(results);
        Assert.Equal("A", results[0].Name);
    }

    [Fact]
    public async Task ListByOwnerAsync_OwnerWithNoSavedSearches_ReturnsEmptyNotNull()
    {
        var repo = new SqliteSavedSearchRepository(_factory);

        var results = await repo.ListByOwnerAsync("nobody");

        Assert.NotNull(results);
        Assert.Empty(results);
    }

    [Fact]
    public async Task DeleteAsync_RemovesOnlyMatchingOwnerAndId()
    {
        var repo = new SqliteSavedSearchRepository(_factory);
        var id = Guid.NewGuid().ToString();
        await repo.CreateAsync(new SavedSearch(id, "alice", "A", "{}", DateTimeOffset.UtcNow));

        await repo.DeleteAsync(id, "bob");
        Assert.Single(await repo.ListByOwnerAsync("alice"));

        await repo.DeleteAsync(id, "alice");
        Assert.Empty(await repo.ListByOwnerAsync("alice"));
    }

    public void Dispose()
    {
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }
}
