using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record LdapsResetPasswordCommand(string Username, string NewPassword, string Scope, string Actor);

public sealed class LdapsResetPasswordUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;
    private readonly INotificationSender _notifications;

    public LdapsResetPasswordUseCase(
        IActiveDirectoryGateway gateway,
        IAuditLogger audit,
        INotificationSender notifications)
    {
        _gateway = gateway;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<OperationResult<string>> ExecuteAsync(LdapsResetPasswordCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return OperationResult<string>.Fail("Username is required.");
        }

        if (string.IsNullOrWhiteSpace(command.NewPassword))
        {
            return OperationResult<string>.Fail("New password is required.");
        }

        try
        {
            await _gateway.ResetPasswordViaLdapsAsync(command.Username, command.NewPassword, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "ResetPasswordLdaps", command.Username, true, null), ct);
            await _notifications.NotifyAsync(new NotificationMessage(
                $"Password Reset (LDAPS): {command.Username}",
                $"Performed by {command.Actor}",
                AlertSeverity.Info,
                new Dictionary<string, string> { ["User"] = command.Username, ["Actor"] = command.Actor },
                DateTimeOffset.UtcNow), ct);
            return OperationResult<string>.Ok(command.Username);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "ResetPasswordLdaps", command.Username, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to reset password via LDAPS: {ex.Message}");
        }
    }
}
