using ADManager.Infrastructure.Storage;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Storage;

public sealed class SqliteSchemaInitializerTests
{
    [Fact]
    public void EnsureCreated_CreatesSessionsAndAuditLogTables()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"ad-manager-test-{Guid.NewGuid()}.db");
        try
        {
            var factory = new SqliteConnectionFactory(Options.Create(new SqliteStorageSettings { DatabasePath = dbPath }));
            new SqliteSchemaInitializer().EnsureCreated(factory);

            using var connection = factory.CreateOpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name IN ('Sessions','AuditLog')";
            using var reader = command.ExecuteReader();
            var tableNames = new List<string>();
            while (reader.Read()) tableNames.Add(reader.GetString(0));

            Assert.Contains("Sessions", tableNames);
            Assert.Contains("AuditLog", tableNames);
        }
        finally
        {
            if (File.Exists(dbPath)) File.Delete(dbPath);
        }
    }
}
