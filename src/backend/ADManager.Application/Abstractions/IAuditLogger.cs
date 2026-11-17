using ADManager.Domain;

namespace ADManager.Application.Abstractions;

public interface IAuditLogger
{
    Task LogAsync(AuditEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQuery query, CancellationToken ct = default);
}

public sealed record AuditQuery(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Actor,
    string? Operation,
    int Page,
    int PageSize);
