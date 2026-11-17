using System.Text.Json;
using ADManager.Application.Abstractions;
using ADManager.Domain;
using ADManager.Infrastructure.Storage;
using Microsoft.Extensions.Options;

namespace ADManager.Infrastructure.Sessions;

public sealed class SqliteSessionService : ISessionService
{
    private readonly SqliteConnectionFactory _connectionFactory;
    private readonly TimeSpan _sessionLifetime;

    public SqliteSessionService(SqliteConnectionFactory connectionFactory, IOptions<SessionSettings> settings)
    {
        _connectionFactory = connectionFactory;
        _sessionLifetime = settings.Value.IdleTimeout;
    }

    public Task<SessionRecord> CreateAsync(string username, string displayName, IReadOnlyList<string> groups, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new SessionRecord(Guid.NewGuid(), username, displayName, groups, now, now + _sessionLifetime);

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Sessions (SessionId, Username, DisplayName, GroupsJson, IssuedAt, ExpiresAt)
            VALUES ($id, $username, $displayName, $groups, $issuedAt, $expiresAt)
            """;
        command.Parameters.AddWithValue("$id", record.Id.ToString());
        command.Parameters.AddWithValue("$username", record.Username);
        command.Parameters.AddWithValue("$displayName", record.DisplayName);
        command.Parameters.AddWithValue("$groups", JsonSerializer.Serialize(record.Groups));
        command.Parameters.AddWithValue("$issuedAt", record.IssuedAt.ToString("O"));
        command.Parameters.AddWithValue("$expiresAt", record.ExpiresAt.ToString("O"));
        command.ExecuteNonQuery();

        return Task.FromResult(record);
    }

    public Task<SessionRecord?> GetAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Username, DisplayName, GroupsJson, IssuedAt, ExpiresAt FROM Sessions WHERE SessionId = $id AND ExpiresAt > $now";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return Task.FromResult<SessionRecord?>(null);
        }

        var record = new SessionRecord(
            sessionId,
            reader.GetString(0),
            reader.GetString(1),
            JsonSerializer.Deserialize<List<string>>(reader.GetString(2))!,
            DateTimeOffset.Parse(reader.GetString(3)),
            DateTimeOffset.Parse(reader.GetString(4)));

        return Task.FromResult<SessionRecord?>(record);
    }

    public Task TouchAsync(Guid sessionId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var newExpiry = now + _sessionLifetime;

        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Sessions SET ExpiresAt = $expiresAt WHERE SessionId = $id AND ExpiresAt > $now";
        command.Parameters.AddWithValue("$expiresAt", newExpiry.ToString("O"));
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.Parameters.AddWithValue("$now", now.ToString("O"));
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Sessions WHERE SessionId = $id";
        command.Parameters.AddWithValue("$id", sessionId.ToString());
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }
}
