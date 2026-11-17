using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.Tests.Fakes;

public sealed class FakeSessionService : ISessionService
{
    public Dictionary<Guid, SessionRecord> Sessions { get; } = new();

    public Task<SessionRecord> CreateAsync(string username, string displayName, IReadOnlyList<string> groups, CancellationToken ct = default)
    {
        var record = new SessionRecord(Guid.NewGuid(), username, displayName, groups, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(8));
        Sessions[record.Id] = record;
        return Task.FromResult(record);
    }

    public Task<SessionRecord?> GetAsync(Guid sessionId, CancellationToken ct = default) =>
        Task.FromResult(Sessions.TryGetValue(sessionId, out var s) ? s : null);

    public Task TouchAsync(Guid sessionId, CancellationToken ct = default) => Task.CompletedTask;

    public Task RemoveAsync(Guid sessionId, CancellationToken ct = default)
    {
        Sessions.Remove(sessionId);
        return Task.CompletedTask;
    }
}
