using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record CreateGroupCommand(string Name, string? Description, string Scope, string Actor);
public sealed record DeleteGroupCommand(string Name, string Scope, string Actor);
public sealed record UpdateGroupDescriptionCommand(string Name, string? Description, string Scope, string Actor);

public sealed class ManageGroupUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;

    public ManageGroupUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit)
    {
        _gateway = gateway;
        _audit = audit;
    }

    public async Task<OperationResult<string>> CreateAsync(CreateGroupCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return OperationResult<string>.Fail("Group name is required.");
        }

        try
        {
            await _gateway.CreateGroupAsync(command.Name, command.Description, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "CreateGroup", command.Name, true, command.Description), ct);
            return OperationResult<string>.Ok($"Group '{command.Name}' created.");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "CreateGroup", command.Name, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to create group: {ex.Message}");
        }
    }

    public async Task<OperationResult<string>> DeleteAsync(DeleteGroupCommand command, CancellationToken ct = default)
    {
        try
        {
            await _gateway.DeleteGroupAsync(command.Name, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "DeleteGroup", command.Name, true, null), ct);
            return OperationResult<string>.Ok($"Group '{command.Name}' deleted.");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "DeleteGroup", command.Name, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to delete group: {ex.Message}");
        }
    }

    public async Task<OperationResult<string>> UpdateDescriptionAsync(UpdateGroupDescriptionCommand command, CancellationToken ct = default)
    {
        try
        {
            await _gateway.SetGroupDescriptionAsync(command.Name, command.Description, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "UpdateGroupDescription", command.Name, true, command.Description), ct);
            return OperationResult<string>.Ok($"Group '{command.Name}' updated.");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "UpdateGroupDescription", command.Name, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to update group: {ex.Message}");
        }
    }
}
