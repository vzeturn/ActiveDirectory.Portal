using ADManager.Application.Abstractions;
using ADManager.Domain;
using Microsoft.Extensions.Caching.Memory;

namespace ADManager.Infrastructure.ActiveDirectory;

public sealed class CachedActiveDirectoryGateway : IActiveDirectoryGateway
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(45);
    private const string UserSearchKind = "user-search";
    private const string GroupSearchKind = "group-search";

    private readonly IActiveDirectoryGateway _inner;
    private readonly IMemoryCache _cache;
    private readonly AdCacheGenerationStore _generations;

    public CachedActiveDirectoryGateway(IActiveDirectoryGateway inner, IMemoryCache cache, AdCacheGenerationStore generations)
    {
        _inner = inner;
        _cache = cache;
        _generations = generations;
    }

    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default) =>
        _inner.ValidateCredentialsAsync(username, password, ct);

    public Task<bool> CanBindAsync(CancellationToken ct = default) =>
        _inner.CanBindAsync(ct);

    public Task<AdUser?> GetUserAsync(string username, string scope, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(UserKey(username, scope), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return _inner.GetUserAsync(username, scope, ct);
        })!;

    public Task<IReadOnlyList<AdUser>> SearchUsersAsync(string query, int limit, string scope, bool includeGroups = true, CancellationToken ct = default)
    {
        var generation = _generations.GetGeneration(scope, UserSearchKind);
        var key = $"search:{UserSearchKind}:{scope}:{generation}:{query}:{limit}:{includeGroups}";
        return _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return _inner.SearchUsersAsync(query, limit, scope, includeGroups, ct);
        })!;
    }

    public async Task EnableUserAsync(string username, string scope, CancellationToken ct = default)
    {
        await _inner.EnableUserAsync(username, scope, ct);
        EvictUser(username, scope);
    }

    public async Task DisableUserAsync(string username, string scope, CancellationToken ct = default)
    {
        await _inner.DisableUserAsync(username, scope, ct);
        EvictUser(username, scope);
    }

    public async Task UnlockUserAsync(string username, string scope, CancellationToken ct = default)
    {
        await _inner.UnlockUserAsync(username, scope, ct);
        EvictUser(username, scope);
    }

    public async Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default)
    {
        await _inner.ResetPasswordAsync(username, newPassword, requireChange, scope, ct);
        EvictUser(username, scope);
    }

    public async Task ResetPasswordViaLdapsAsync(string username, string newPassword, string scope, CancellationToken ct = default)
    {
        await _inner.ResetPasswordViaLdapsAsync(username, newPassword, scope, ct);
        EvictUser(username, scope);
    }

    public Task<AdGroup?> GetGroupAsync(string groupName, string scope, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(GroupKey(groupName, scope), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return _inner.GetGroupAsync(groupName, scope, ct);
        })!;

    public Task<IReadOnlyList<AdGroup>> SearchGroupsAsync(string query, int limit, string scope, CancellationToken ct = default)
    {
        var generation = _generations.GetGeneration(scope, GroupSearchKind);
        var key = $"search:{GroupSearchKind}:{scope}:{generation}:{query}:{limit}";
        return _cache.GetOrCreateAsync(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return _inner.SearchGroupsAsync(query, limit, scope, ct);
        })!;
    }

    public async Task CreateGroupAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        await _inner.CreateGroupAsync(name, description, scope, ct);
        EvictGroup(name, scope);
    }

    public async Task DeleteGroupAsync(string name, string scope, CancellationToken ct = default)
    {
        await _inner.DeleteGroupAsync(name, scope, ct);
        EvictGroup(name, scope);
    }

    public async Task SetGroupDescriptionAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        await _inner.SetGroupDescriptionAsync(name, description, scope, ct);
        EvictGroup(name, scope);
    }

    public Task<IReadOnlyList<string>> GetGroupMembershipAsync(string username, string scope, CancellationToken ct = default) =>
        _cache.GetOrCreateAsync(MembershipKey(username, scope), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            return _inner.GetGroupMembershipAsync(username, scope, ct);
        })!;

    public async Task AddUserToGroupsAsync(string username, IReadOnlyList<string> groupNames, string scope, CancellationToken ct = default)
    {
        await _inner.AddUserToGroupsAsync(username, groupNames, scope, ct);
        EvictUser(username, scope);
        _cache.Remove(MembershipKey(username, scope));
        foreach (var groupName in groupNames)
        {
            EvictGroup(groupName, scope);
        }
        _generations.BumpGeneration(scope, UserSearchKind);
        _generations.BumpGeneration(scope, GroupSearchKind);
    }

    public async Task RemoveUserFromGroupAsync(string username, string groupName, string scope, CancellationToken ct = default)
    {
        await _inner.RemoveUserFromGroupAsync(username, groupName, scope, ct);
        EvictUser(username, scope);
        _cache.Remove(MembershipKey(username, scope));
        EvictGroup(groupName, scope);
        _generations.BumpGeneration(scope, UserSearchKind);
        _generations.BumpGeneration(scope, GroupSearchKind);
    }

    private void EvictUser(string username, string scope)
    {
        _cache.Remove(UserKey(username, scope));
        _generations.BumpGeneration(scope, UserSearchKind);
    }

    private void EvictGroup(string groupName, string scope)
    {
        _cache.Remove(GroupKey(groupName, scope));
        _generations.BumpGeneration(scope, GroupSearchKind);
    }

    private static string UserKey(string username, string scope) => $"user:{scope}:{username}";
    private static string GroupKey(string groupName, string scope) => $"group:{scope}:{groupName}";
    private static string MembershipKey(string username, string scope) => $"membership:{scope}:{username}";
}
