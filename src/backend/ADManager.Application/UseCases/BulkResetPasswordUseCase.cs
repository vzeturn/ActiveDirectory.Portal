using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record BulkResetPasswordCommand(IReadOnlyList<string> Usernames, bool RequireChange, string Scope, string Actor);

public sealed class BulkResetPasswordUseCase
{
    private readonly ResetPasswordUseCase _reset;

    public BulkResetPasswordUseCase(ResetPasswordUseCase reset)
    {
        _reset = reset;
    }

    public async Task<OperationResult<IReadOnlyList<BulkOpItemResult>>> ExecuteAsync(BulkResetPasswordCommand command, CancellationToken ct = default)
    {
        if (command.Usernames.Count == 0)
        {
            return OperationResult<IReadOnlyList<BulkOpItemResult>>.Fail("At least one username is required.");
        }

        var results = new List<BulkOpItemResult>();
        foreach (var username in command.Usernames)
        {
            var result = await _reset.ExecuteAsync(new ResetPasswordCommand(username, null, command.RequireChange, command.Scope, command.Actor), ct);
            results.Add(new BulkOpItemResult(username, result.Success, result.Success ? null : result.Error));
        }

        return OperationResult<IReadOnlyList<BulkOpItemResult>>.Ok(results);
    }
}
