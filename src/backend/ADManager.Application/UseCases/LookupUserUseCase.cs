using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record SearchUsersQuery(
    string Query,
    int Limit,
    string Scope,
    string Actor,
    string? OuFilter = null,
    bool? EnabledOnly = null,
    bool? LockedOnly = null,
    string? MemberOfGroup = null);
public sealed record GetUserQuery(string Username, string Scope, string Actor);

public sealed class LookupUserUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;

    public LookupUserUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit)
    {
        _gateway = gateway;
        _audit = audit;
    }

    public async Task<OperationResult<IReadOnlyList<AdUser>>> SearchAsync(SearchUsersQuery query, CancellationToken ct = default)
    {
        try
        {
            var users = await _gateway.SearchUsersAsync(query.Query, query.Limit, query.Scope, ct: ct);

            IEnumerable<AdUser> filtered = users;
            if (query.EnabledOnly is true) filtered = filtered.Where(u => u.Enabled);
            if (query.LockedOnly is true) filtered = filtered.Where(u => u.IsLockedOut);
            if (!string.IsNullOrWhiteSpace(query.OuFilter))
                filtered = filtered.Where(u => (u.DistinguishedName ?? string.Empty).Contains(query.OuFilter, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(query.MemberOfGroup))
                filtered = filtered.Where(u => u.Groups.Contains(query.MemberOfGroup, StringComparer.OrdinalIgnoreCase));

            var finalUsers = filtered.ToList();
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "SearchUsers", query.Query, true, $"{finalUsers.Count} results"), ct);
            return OperationResult<IReadOnlyList<AdUser>>.Ok(finalUsers);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "SearchUsers", query.Query, false, ex.Message), ct);
            return OperationResult<IReadOnlyList<AdUser>>.Fail("Failed to search users.");
        }
    }

    public async Task<OperationResult<AdUser?>> GetAsync(GetUserQuery query, CancellationToken ct = default)
    {
        try
        {
            var user = await _gateway.GetUserAsync(query.Username, query.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "GetUser", query.Username, true, user is null ? "Not found" : null), ct);
            return OperationResult<AdUser?>.Ok(user);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "GetUser", query.Username, false, ex.Message), ct);
            return OperationResult<AdUser?>.Fail("Failed to look up user.");
        }
    }
}
