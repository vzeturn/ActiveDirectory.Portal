using ADManager.Domain;

namespace ADManager.Application.Abstractions;

public interface IActiveDirectoryGateway
{
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default);
    Task<AdUser?> GetUserAsync(string username, string scope, CancellationToken ct = default);
    Task<IReadOnlyList<AdUser>> SearchUsersAsync(string query, int limit, string scope, bool includeGroups = true, CancellationToken ct = default);
    Task EnableUserAsync(string username, string scope, CancellationToken ct = default);
    Task DisableUserAsync(string username, string scope, CancellationToken ct = default);
    Task UnlockUserAsync(string username, string scope, CancellationToken ct = default);
    Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default);
    Task ResetPasswordViaLdapsAsync(string username, string newPassword, string scope, CancellationToken ct = default);
    Task<AdGroup?> GetGroupAsync(string groupName, string scope, CancellationToken ct = default);
    Task<IReadOnlyList<AdGroup>> SearchGroupsAsync(string query, int limit, string scope, CancellationToken ct = default);
    Task CreateGroupAsync(string name, string? description, string scope, CancellationToken ct = default);
    Task DeleteGroupAsync(string name, string scope, CancellationToken ct = default);
    Task SetGroupDescriptionAsync(string name, string? description, string scope, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetGroupMembershipAsync(string username, string scope, CancellationToken ct = default);
    Task AddUserToGroupsAsync(string username, IReadOnlyList<string> groupNames, string scope, CancellationToken ct = default);
    Task RemoveUserFromGroupAsync(string username, string groupName, string scope, CancellationToken ct = default);
    Task<bool> CanBindAsync(CancellationToken ct = default);
}
