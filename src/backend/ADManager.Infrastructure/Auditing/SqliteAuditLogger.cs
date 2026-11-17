using ADManager.Application.Abstractions;
using ADManager.Domain;
using ADManager.Infrastructure.Storage;

namespace ADManager.Infrastructure.Auditing;

public sealed class SqliteAuditLogger : IAuditLogger
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteAuditLogger(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO AuditLog (Timestamp, Actor, Operation, Target, Result, Details)
            VALUES ($timestamp, $actor, $operation, $target, $result, $details)
            """;
        command.Parameters.AddWithValue("$timestamp", entry.Timestamp.ToUniversalTime().ToString("O"));
        command.Parameters.AddWithValue("$actor", entry.Actor);
        command.Parameters.AddWithValue("$operation", entry.Operation);
        command.Parameters.AddWithValue("$target", entry.Target);
        command.Parameters.AddWithValue("$result", entry.Result ? 1 : 0);
        command.Parameters.AddWithValue("$details", (object?)entry.Details ?? DBNull.Value);
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();

        var conditions = new List<string>();
        if (query.From is not null) conditions.Add("Timestamp >= $from");
        if (query.To is not null) conditions.Add("Timestamp <= $to");
        if (!string.IsNullOrWhiteSpace(query.Actor)) conditions.Add("Actor = $actor");
        if (!string.IsNullOrWhiteSpace(query.Operation)) conditions.Add("Operation = $operation");

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Max(query.PageSize, 1);
        var offset = (page - 1) * pageSize;

        command.CommandText = $"""
            SELECT Timestamp, Actor, Operation, Target, Result, Details
            FROM AuditLog
            {whereClause}
            ORDER BY Timestamp DESC
            LIMIT $pageSize OFFSET $offset
            """;

        if (query.From is not null) command.Parameters.AddWithValue("$from", query.From.Value.ToUniversalTime().ToString("O"));
        if (query.To is not null) command.Parameters.AddWithValue("$to", query.To.Value.ToUniversalTime().ToString("O"));
        if (!string.IsNullOrWhiteSpace(query.Actor)) command.Parameters.AddWithValue("$actor", query.Actor);
        if (!string.IsNullOrWhiteSpace(query.Operation)) command.Parameters.AddWithValue("$operation", query.Operation);
        command.Parameters.AddWithValue("$pageSize", pageSize);
        command.Parameters.AddWithValue("$offset", offset);

        using var reader = command.ExecuteReader();
        var results = new List<AuditEntry>();
        while (reader.Read())
        {
            results.Add(new AuditEntry(
                DateTimeOffset.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4) == 1,
                reader.IsDBNull(5) ? null : reader.GetString(5)));
        }

        return Task.FromResult<IReadOnlyList<AuditEntry>>(results);
    }
}
