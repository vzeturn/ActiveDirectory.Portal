using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record AccountCommand(string Username, string Scope, string Actor);

public sealed class ToggleAccountUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;
    private readonly INotificationSender _notifications;

    public ToggleAccountUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit, INotificationSender notifications)
    {
        _gateway = gateway;
        _audit = audit;
        _notifications = notifications;
    }

    public Task<OperationResult<string>> EnableAsync(AccountCommand command, CancellationToken ct = default) =>
        RunAsync(command, "EnableAccount", () => _gateway.EnableUserAsync(command.Username, command.Scope, ct), ct);

    public Task<OperationResult<string>> DisableAsync(AccountCommand command, CancellationToken ct = default) =>
        RunAsync(command, "DisableAccount", () => _gateway.DisableUserAsync(command.Username, command.Scope, ct), ct);

    public Task<OperationResult<string>> UnlockAsync(AccountCommand command, CancellationToken ct = default) =>
        RunAsync(command, "UnlockAccount", () => _gateway.UnlockUserAsync(command.Username, command.Scope, ct), ct);

    private async Task<OperationResult<string>> RunAsync(AccountCommand command, string operation, Func<Task> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return OperationResult<string>.Fail("Username is required.");
        }

        try
        {
            await action();
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, operation, command.Username, true, null), ct);
            await _notifications.NotifyAsync(new NotificationMessage(
                $"{operation}: {command.Username}",
                $"Performed by {command.Actor}",
                true,
                new Dictionary<string, string> { ["User"] = command.Username, ["Actor"] = command.Actor },
                DateTimeOffset.UtcNow), ct);
            return OperationResult<string>.Ok($"{operation} succeeded for '{command.Username}'.");
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, operation, command.Username, false, ex.Message), ct);
            return OperationResult<string>.Fail($"{operation} failed: {ex.Message}");
        }
    }
}
