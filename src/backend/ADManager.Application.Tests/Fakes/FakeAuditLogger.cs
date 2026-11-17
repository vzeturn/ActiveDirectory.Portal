using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.Tests.Fakes;

public sealed class FakeAuditLogger : IAuditLogger
{
    public List<AuditEntry> Entries { get; } = new();

    public Task LogAsync(AuditEntry entry, CancellationToken ct = default)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<AuditEntry>>(Entries.ToList());
}
