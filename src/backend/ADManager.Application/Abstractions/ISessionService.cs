using ADManager.Domain;

namespace ADManager.Application.Abstractions;

public interface ISessionService
{
    Task<SessionRecord> CreateAsync(string username, string displayName, IReadOnlyList<string> groups, CancellationToken ct = default);
    Task<SessionRecord?> GetAsync(Guid sessionId, CancellationToken ct = default);
    Task TouchAsync(Guid sessionId, CancellationToken ct = default);
    Task RemoveAsync(Guid sessionId, CancellationToken ct = default);
}
