using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record BulkGroupAssignmentCommand(IReadOnlyList<string> Usernames, IReadOnlyList<string> GroupNames, string Scope, string Actor);

public sealed class BulkGroupAssignmentUseCase
{
    private readonly ManageGroupMembershipUseCase _membership;

    public BulkGroupAssignmentUseCase(ManageGroupMembershipUseCase membership)
    {
        _membership = membership;
    }

    public async Task<OperationResult<IReadOnlyList<BulkOpItemResult>>> ExecuteAsync(BulkGroupAssignmentCommand command, CancellationToken ct = default)
    {
        if (command.Usernames.Count == 0)
        {
            return OperationResult<IReadOnlyList<BulkOpItemResult>>.Fail("At least one username is required.");
        }
        if (command.GroupNames.Count == 0)
        {
            return OperationResult<IReadOnlyList<BulkOpItemResult>>.Fail("At least one group name is required.");
        }

        var results = new List<BulkOpItemResult>();
        foreach (var username in command.Usernames)
        {
            var result = await _membership.AddAsync(new ChangeGroupMembershipCommand(username, command.GroupNames, command.Scope, command.Actor), ct);
            results.Add(new BulkOpItemResult(username, result.Success, result.Success ? null : result.Error));
        }

        return OperationResult<IReadOnlyList<BulkOpItemResult>>.Ok(results);
    }
}
