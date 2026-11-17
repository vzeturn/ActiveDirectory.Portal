using ADManager.Domain;

namespace ADManager.Application.Abstractions;

public interface ISavedSearchRepository
{
    Task<string> CreateAsync(SavedSearch search, CancellationToken ct = default);
    Task<IReadOnlyList<SavedSearch>> ListByOwnerAsync(string owner, CancellationToken ct = default);
    Task DeleteAsync(string id, string owner, CancellationToken ct = default);
}
