using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record BulkAccountOpCommand(IReadOnlyList<string> Usernames, string Operation, string Scope, string Actor);
public sealed record BulkOpItemResult(string Username, bool Success, string? Error);

public sealed class BulkAccountOpUseCase
{
    private readonly ToggleAccountUseCase _toggle;

    public BulkAccountOpUseCase(ToggleAccountUseCase toggle)
    {
        _toggle = toggle;
    }

    public async Task<OperationResult<IReadOnlyList<BulkOpItemResult>>> ExecuteAsync(BulkAccountOpCommand command, CancellationToken ct = default)
    {
        if (command.Usernames.Count == 0)
        {
            return OperationResult<IReadOnlyList<BulkOpItemResult>>.Fail("At least one username is required.");
        }

        Func<AccountCommand, CancellationToken, Task<OperationResult<string>>>? op = command.Operation.ToLowerInvariant() switch
        {
            "enable" => _toggle.EnableAsync,
            "disable" => _toggle.DisableAsync,
            "unlock" => _toggle.UnlockAsync,
            _ => null
        };

        if (op is null)
        {
            return OperationResult<IReadOnlyList<BulkOpItemResult>>.Fail($"Unknown bulk operation '{command.Operation}'.");
        }

        var results = new List<BulkOpItemResult>();
        foreach (var username in command.Usernames)
        {
            var result = await op(new AccountCommand(username, command.Scope, command.Actor), ct);
            results.Add(new BulkOpItemResult(username, result.Success, result.Success ? null : result.Error));
        }

        return OperationResult<IReadOnlyList<BulkOpItemResult>>.Ok(results);
    }
}
