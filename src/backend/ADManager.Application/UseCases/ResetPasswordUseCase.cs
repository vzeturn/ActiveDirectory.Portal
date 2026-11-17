using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record ResetPasswordCommand(string Username, string? NewPassword, bool RequireChange, string Scope, string Actor);

public sealed class ResetPasswordUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IPasswordGenerator _passwordGenerator;
    private readonly IAuditLogger _audit;
    private readonly INotificationSender _notifications;

    public ResetPasswordUseCase(
        IActiveDirectoryGateway gateway,
        IPasswordGenerator passwordGenerator,
        IAuditLogger audit,
        INotificationSender notifications)
    {
        _gateway = gateway;
        _passwordGenerator = passwordGenerator;
        _audit = audit;
        _notifications = notifications;
    }

    public async Task<OperationResult<string>> ExecuteAsync(ResetPasswordCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username))
        {
            return OperationResult<string>.Fail("Username is required.");
        }

        if (!string.IsNullOrWhiteSpace(command.NewPassword))
        {
            var complexityError = PasswordComplexity.Validate(command.NewPassword);
            if (complexityError is not null)
            {
                return OperationResult<string>.Fail(complexityError);
            }
        }

        var newPassword = string.IsNullOrWhiteSpace(command.NewPassword)
            ? _passwordGenerator.Generate()
            : command.NewPassword;

        try
        {
            await _gateway.ResetPasswordAsync(command.Username, newPassword, command.RequireChange, command.Scope, ct);
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "ResetPassword", command.Username, true, null), ct);
            await _notifications.NotifyAsync(new NotificationMessage(
                $"Password Reset: {command.Username}",
                $"Performed by {command.Actor}",
                true,
                new Dictionary<string, string> { ["User"] = command.Username, ["Actor"] = command.Actor, ["RequireChange"] = command.RequireChange.ToString(), ["NewPassword"] = newPassword },
                DateTimeOffset.UtcNow), ct);
            return OperationResult<string>.Ok(newPassword);
        }
        catch (PasswordSetButFollowUpFailedException ex)
        {
            // The password itself was already changed on AD before the failure - the
            // operator must be told the new password even though the operation is
            // reported as a partial failure, otherwise it is lost with no way to recover it.
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "ResetPassword", command.Username, true,
                $"Password was set but a follow-up step failed: {ex.InnerException?.Message}"), ct);
            await _notifications.NotifyAsync(new NotificationMessage(
                $"Password Reset (partial): {command.Username}",
                $"Performed by {command.Actor}. Password was changed but a follow-up step failed: {ex.InnerException?.Message}",
                true,
                new Dictionary<string, string> { ["User"] = command.Username, ["Actor"] = command.Actor, ["RequireChange"] = command.RequireChange.ToString(), ["NewPassword"] = ex.NewPassword },
                DateTimeOffset.UtcNow), ct);
            return OperationResult<string>.Ok(ex.NewPassword);
        }
        catch (Exception ex)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Actor, "ResetPassword", command.Username, false, ex.Message), ct);
            return OperationResult<string>.Fail($"Failed to reset password: {ex.Message}");
        }
    }
}
