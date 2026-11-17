using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record SearchGroupsQuery(string Query, int Limit, string Scope, string Actor, string? OuFilter = null);
public sealed record GetGroupQuery(string GroupName, string Scope, string Actor);

public sealed class LookupGroupUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;

    public LookupGroupUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit)
    {
        _gateway = gateway;
        _audit = audit;
    }

    public async Task<OperationResult<IReadOnlyList<AdGroup>>> SearchAsync(SearchGroupsQuery query, CancellationToken ct = default)
    {
        try
        {
            var groups = await _gateway.SearchGroupsAsync(query.Query, query.Limit, query.Scope, ct);

            IEnumerable<AdGroup> filtered = groups;
            if (!string.IsNullOrWhiteSpace(query.OuFilter))
                filtered = filtered.Where(g => (g.DistinguishedName ?? string.Empty).Contains(query.OuFilter, StringComparison.OrdinalIgnoreCase));

            var finalGroups = filtered.ToList();
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "SearchGroups", query.Query, true, $"{finalGroups.Count} results"), ct);
            return OperationResult<IReadOnlyList<AdGroup>>.Ok(finalGroups);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "SearchGroups", query.Query, false, ex.Message), ct);
            return OperationResult<IReadOnlyList<AdGroup>>.Fail("Failed to search groups.");
        }
    }

    public async Task<OperationResult<AdGroup?>> GetAsync(GetGroupQuery query, CancellationToken ct = default)
    {
        try
        {
            var group = await _gateway.GetGroupAsync(query.GroupName, query.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "GetGroup", query.GroupName, true, group is null ? "Not found" : null), ct);
            return OperationResult<AdGroup?>.Ok(group);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "GetGroup", query.GroupName, false, ex.Message), ct);
            return OperationResult<AdGroup?>.Fail("Failed to look up group.");
        }
    }
}
