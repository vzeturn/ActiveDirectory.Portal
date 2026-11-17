using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.Tests.Fakes;

public class FakeActiveDirectoryGateway : IActiveDirectoryGateway
{
    public List<AdUser> Users { get; } = new();
    public List<AdGroup> Groups { get; } = new();
    public Dictionary<string, string> Credentials { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> Memberships { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool CanBind { get; set; } = true;

    public Dictionary<string, int> CallCounts { get; } = new();
    public bool? LastIncludeGroups { get; private set; }

    private void RecordCall(string method) =>
        CallCounts[method] = CallCounts.GetValueOrDefault(method) + 1;

    public virtual Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        RecordCall(nameof(ValidateCredentialsAsync));
        return Task.FromResult(Credentials.TryGetValue(username, out var expected) && expected == password);
    }

    public virtual Task<AdUser?> GetUserAsync(string username, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(GetUserAsync));
        return Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase)));
    }

    public virtual Task<IReadOnlyList<AdUser>> SearchUsersAsync(string query, int limit, string scope, bool includeGroups = true, CancellationToken ct = default)
    {
        RecordCall(nameof(SearchUsersAsync));
        LastIncludeGroups = includeGroups;
        return Task.FromResult<IReadOnlyList<AdUser>>(Users
            .Where(u => u.Username.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (u.DisplayName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(limit)
            .ToList());
    }

    public virtual Task EnableUserAsync(string username, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(EnableUserAsync));
        return SetEnabled(username, true);
    }

    public virtual Task DisableUserAsync(string username, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(DisableUserAsync));
        return SetEnabled(username, false);
    }

    public virtual Task UnlockUserAsync(string username, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(UnlockUserAsync));
        var idx = Users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) Users[idx] = Users[idx] with { IsLockedOut = false };
        return Task.CompletedTask;
    }

    public virtual Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(ResetPasswordAsync));
        Credentials[username] = newPassword;
        return Task.CompletedTask;
    }

    public virtual Task ResetPasswordViaLdapsAsync(string username, string newPassword, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(ResetPasswordViaLdapsAsync));
        Credentials[username] = newPassword;
        return Task.CompletedTask;
    }

    public virtual Task<AdGroup?> GetGroupAsync(string groupName, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(GetGroupAsync));
        return Task.FromResult(Groups.FirstOrDefault(g => string.Equals(g.Name, groupName, StringComparison.OrdinalIgnoreCase)));
    }

    public virtual Task<IReadOnlyList<AdGroup>> SearchGroupsAsync(string query, int limit, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(SearchGroupsAsync));
        return Task.FromResult<IReadOnlyList<AdGroup>>(Groups
            .Where(g => g.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(limit)
            .ToList());
    }

    public virtual Task CreateGroupAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(CreateGroupAsync));
        if (Groups.Any(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Group '{name}' already exists.");
        }
        Groups.Add(new AdGroup { Name = name, Description = description });
        return Task.CompletedTask;
    }

    public virtual Task DeleteGroupAsync(string name, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(DeleteGroupAsync));
        var idx = Groups.FindIndex(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new InvalidOperationException($"Group '{name}' was not found.");
        Groups.RemoveAt(idx);
        return Task.CompletedTask;
    }

    public virtual Task SetGroupDescriptionAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(SetGroupDescriptionAsync));
        var idx = Groups.FindIndex(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new InvalidOperationException($"Group '{name}' was not found.");
        Groups[idx] = Groups[idx] with { Description = description };
        return Task.CompletedTask;
    }

    public virtual Task<IReadOnlyList<string>> GetGroupMembershipAsync(string username, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(GetGroupMembershipAsync));
        return Task.FromResult<IReadOnlyList<string>>(
            Memberships.TryGetValue(username, out var groups) ? groups : new List<string>());
    }

    public virtual Task AddUserToGroupsAsync(string username, IReadOnlyList<string> groupNames, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(AddUserToGroupsAsync));
        if (!Memberships.TryGetValue(username, out var groups))
        {
            groups = new List<string>();
            Memberships[username] = groups;
        }
        foreach (var g in groupNames)
        {
            if (!groups.Contains(g, StringComparer.OrdinalIgnoreCase)) groups.Add(g);
        }
        return Task.CompletedTask;
    }

    public virtual Task RemoveUserFromGroupAsync(string username, string groupName, string scope, CancellationToken ct = default)
    {
        RecordCall(nameof(RemoveUserFromGroupAsync));
        if (Memberships.TryGetValue(username, out var groups))
        {
            groups.RemoveAll(g => string.Equals(g, groupName, StringComparison.OrdinalIgnoreCase));
        }
        return Task.CompletedTask;
    }

    public virtual Task<bool> CanBindAsync(CancellationToken ct = default)
    {
        RecordCall(nameof(CanBindAsync));
        return Task.FromResult(CanBind);
    }

    private Task SetEnabled(string username, bool enabled)
    {
        var idx = Users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx >= 0) Users[idx] = Users[idx] with { Enabled = enabled };
        return Task.CompletedTask;
    }
}
