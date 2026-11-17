using ADManager.Application.Abstractions;
using ADManager.Domain;
using ADManager.Infrastructure.Auditing;
using ADManager.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Auditing;

public sealed class SqliteAuditLoggerTests
{
    private static SqliteAuditLogger CreateLogger(out string dbPath)
    {
        dbPath = Path.Combine(Path.GetTempPath(), $"ad-manager-test-{Guid.NewGuid()}.db");
        var factory = new SqliteConnectionFactory(Options.Create(new SqliteStorageSettings { DatabasePath = dbPath }));
        new SqliteSchemaInitializer().EnsureCreated(factory);
        return new SqliteAuditLogger(factory);
    }

    [Fact]
    public async Task LogAsync_ThenQueryAsync_ReturnsEntry()
    {
        var logger = CreateLogger(out var dbPath);
        try
        {
            var entry = new AuditEntry(DateTimeOffset.UtcNow, "jdoe", "ResetPassword", "asmith", true, "reset via admin console");
            await logger.LogAsync(entry);

            var results = await logger.QueryAsync(new AuditQuery(null, null, null, null, Page: 1, PageSize: 10));

            Assert.Single(results);
            Assert.Equal("jdoe", results[0].Actor);
            Assert.Equal("ResetPassword", results[0].Operation);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task QueryAsync_FiltersByActor()
    {
        var logger = CreateLogger(out var dbPath);
        try
        {
            await logger.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, "jdoe", "Login", "jdoe", true, null));
            await logger.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, "asmith", "Login", "asmith", true, null));

            var results = await logger.QueryAsync(new AuditQuery(null, null, "asmith", null, Page: 1, PageSize: 10));

            Assert.Single(results);
            Assert.Equal("asmith", results[0].Actor);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task QueryAsync_FromWithNonUtcOffset_StillMatchesEntry()
    {
        var logger = CreateLogger(out var dbPath);
        try
        {
            var entryTime = DateTimeOffset.UtcNow;
            await logger.LogAsync(new AuditEntry(entryTime, "jdoe", "Login", "jdoe", true, null));

            // Bound expressed in a non-UTC (+07:00) offset, one hour before the entry when normalized to UTC.
            var from = entryTime.AddHours(-1).ToOffset(TimeSpan.FromHours(7));

            var results = await logger.QueryAsync(new AuditQuery(from, null, null, null, Page: 1, PageSize: 10));

            Assert.Single(results);
            Assert.Equal("jdoe", results[0].Actor);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }

    [Fact]
    public async Task QueryAsync_RespectsPageSize()
    {
        var logger = CreateLogger(out var dbPath);
        try
        {
            for (var i = 0; i < 5; i++)
            {
                await logger.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, "jdoe", "Login", "jdoe", true, null));
            }

            var results = await logger.QueryAsync(new AuditQuery(null, null, null, null, Page: 1, PageSize: 2));

            Assert.Equal(2, results.Count);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
