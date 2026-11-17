using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record GroupMembershipQuery(string Username, string Scope, string Actor);
public sealed record ChangeGroupMembershipCommand(string Username, IReadOnlyList<string> GroupNames, string Scope, string Actor);

public sealed class ManageGroupMembershipUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;

    public ManageGroupMembershipUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit)
    {
        _gateway = gateway;
        _audit = audit;
    }

    public async Task<OperationResult<IReadOnlyList<string>>> GetMembershipAsync(GroupMembershipQuery query, CancellationToken ct = default)
    {
        try
        {
            var groups = await _gateway.GetGroupMembershipAsync(query.Username, query.Scope, ct);
            return OperationResult<IReadOnlyList<string>>.Ok(groups);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, query.Actor, "GetGroupMembership", query.Username, false, ex.Message), ct);
            return OperationResult<IReadOnlyList<string>>.Fail("Failed to read group membership.");
        }
    }

    public async Task<OperationResult<string>> AddAsync(ChangeGroupMembershipCommand command, CancellationToken ct = default)
    {
        if (command.GroupNames.Count == 0)
        {
            return OperationResult<string>.Fail("At least one group name is required.");
        }

        try
        {
            await _gateway.AddUserToGroupsAsync(command.Username, command.GroupNames, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "AddToGroups", command.Username, true, string.Join(",", command.GroupNames)), ct);
            return OperationResult<string>.Ok($"Added '{command.Username}' to {command.GroupNames.Count} group(s).");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "AddToGroups", command.Username, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to add to groups: {ex.Message}");
        }
    }

    public async Task<OperationResult<string>> RemoveAsync(ChangeGroupMembershipCommand command, CancellationToken ct = default)
    {
        if (command.GroupNames.Count == 0)
        {
            return OperationResult<string>.Fail("A group name is required.");
        }

        try
        {
            foreach (var groupName in command.GroupNames)
            {
                await _gateway.RemoveUserFromGroupAsync(command.Username, groupName, command.Scope, ct);
            }
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "RemoveFromGroup", command.Username, true, string.Join(",", command.GroupNames)), ct);
            return OperationResult<string>.Ok($"Removed '{command.Username}' from {command.GroupNames.Count} group(s).");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "RemoveFromGroup", command.Username, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to remove from group: {ex.Message}");
        }
    }
}
