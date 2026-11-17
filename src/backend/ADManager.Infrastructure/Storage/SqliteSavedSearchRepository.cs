using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Infrastructure.Storage;

public sealed class SqliteSavedSearchRepository : ISavedSearchRepository
{
    private readonly SqliteConnectionFactory _connectionFactory;

    public SqliteSavedSearchRepository(SqliteConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public Task<string> CreateAsync(SavedSearch search, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SavedSearches (Id, Owner, Name, FilterJson, CreatedAt)
            VALUES ($id, $owner, $name, $filterJson, $createdAt)
            """;
        command.Parameters.AddWithValue("$id", search.Id);
        command.Parameters.AddWithValue("$owner", search.Owner);
        command.Parameters.AddWithValue("$name", search.Name);
        command.Parameters.AddWithValue("$filterJson", search.FilterJson);
        command.Parameters.AddWithValue("$createdAt", search.CreatedAt.ToUniversalTime().ToString("O"));
        command.ExecuteNonQuery();

        return Task.FromResult(search.Id);
    }

    public Task<IReadOnlyList<SavedSearch>> ListByOwnerAsync(string owner, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Owner, Name, FilterJson, CreatedAt
            FROM SavedSearches
            WHERE Owner = $owner
            ORDER BY CreatedAt DESC
            """;
        command.Parameters.AddWithValue("$owner", owner);

        using var reader = command.ExecuteReader();
        var results = new List<SavedSearch>();
        while (reader.Read())
        {
            results.Add(new SavedSearch(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4))));
        }

        return Task.FromResult<IReadOnlyList<SavedSearch>>(results);
    }

    public Task DeleteAsync(string id, string owner, CancellationToken ct = default)
    {
        using var connection = _connectionFactory.CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SavedSearches WHERE Id = $id AND Owner = $owner";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$owner", owner);
        command.ExecuteNonQuery();

        return Task.CompletedTask;
    }
}
