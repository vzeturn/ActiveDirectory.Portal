using ADManager.Infrastructure.Sessions;
using ADManager.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Sessions;

public sealed class SqliteSessionServiceTests
{
    private static SqliteSessionService CreateService(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"ad-manager-test-{Guid.NewGuid()}.db");
        var factory = new SqliteConnectionFactory(Options.Create(new SqliteStorageSettings { DatabasePath = dbPath }));
        new SqliteSchemaInitializer().EnsureCreated(factory);
        return new SqliteSessionService(factory, Options.Create(new SessionSettings()));
    }

    [Fact]
    public async Task CreateAsync_ThenGetAsync_ReturnsSameSession()
    {
        var service = CreateService(out var dbPath);
        try
        {
            var created = await service.CreateAsync("jdoe", "Jane Doe", new[] { "Domain Admins" });

            var fetched = await service.GetAsync(created.Id);

            Assert.NotNull(fetched);
            Assert.Equal("jdoe", fetched!.Username);
            Assert.Equal("Jane Doe", fetched.DisplayName);
            Assert.Equal(new[] { "Domain Admins" }, fetched.Groups);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task RemoveAsync_ThenGetAsync_ReturnsNull()
    {
        var service = CreateService(out var dbPath);
        try
        {
            var created = await service.CreateAsync("jdoe", "Jane Doe", Array.Empty<string>());
            await service.RemoveAsync(created.Id);

            var fetched = await service.GetAsync(created.Id);

            Assert.Null(fetched);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task GetAsync_UnknownId_ReturnsNull()
    {
        var service = CreateService(out var dbPath);
        try
        {
            var fetched = await service.GetAsync(Guid.NewGuid());
            Assert.Null(fetched);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task GetAsync_ExpiredSession_ReturnsNull()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ad-manager-test-{Guid.NewGuid()}.db");
        try
        {
            var factory = new SqliteConnectionFactory(Options.Create(new SqliteStorageSettings { DatabasePath = dbPath }));
            new SqliteSchemaInitializer().EnsureCreated(factory);
            var service = new SqliteSessionService(factory, Options.Create(new SessionSettings()));

            var created = await service.CreateAsync("jdoe", "Jane Doe", Array.Empty<string>());

            using (var connection = factory.CreateOpenConnection())
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "UPDATE Sessions SET ExpiresAt = $expiresAt WHERE SessionId = $id";
                command.Parameters.AddWithValue("$expiresAt", DateTimeOffset.UtcNow.AddHours(-1).ToString("O"));
                command.Parameters.AddWithValue("$id", created.Id.ToString());
                command.ExecuteNonQuery();
            }

            var fetched = await service.GetAsync(created.Id);

            Assert.Null(fetched);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
