namespace ADManager.Infrastructure.Storage;

public sealed class SqliteSchemaInitializer
{
    public void EnsureCreated(SqliteConnectionFactory factory)
    {
        using var connection = factory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS Sessions (
                SessionId TEXT PRIMARY KEY,
                Username TEXT NOT NULL,
                DisplayName TEXT NOT NULL,
                GroupsJson TEXT NOT NULL,
                IssuedAt TEXT NOT NULL,
                ExpiresAt TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Timestamp TEXT NOT NULL,
                Actor TEXT NOT NULL,
                Operation TEXT NOT NULL,
                Target TEXT NOT NULL,
                Result INTEGER NOT NULL,
                Details TEXT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Timestamp ON AuditLog (Timestamp);
            CREATE INDEX IF NOT EXISTS IX_AuditLog_Actor ON AuditLog (Actor);

            CREATE TABLE IF NOT EXISTS SavedSearches (
                Id TEXT PRIMARY KEY,
                Owner TEXT NOT NULL,
                Name TEXT NOT NULL,
                FilterJson TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_SavedSearches_Owner ON SavedSearches (Owner);
            """;
        command.ExecuteNonQuery();
    }
}
