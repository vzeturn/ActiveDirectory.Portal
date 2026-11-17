using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record LoginCommand(string Username, string Password);

public sealed class LoginUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly ISessionService _sessions;
    private readonly IAuditLogger _audit;

    public LoginUseCase(IActiveDirectoryGateway gateway, ISessionService sessions, IAuditLogger audit)
    {
        _gateway = gateway;
        _sessions = sessions;
        _audit = audit;
    }

    public async Task<OperationResult<SessionRecord>> ExecuteAsync(LoginCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Username) || string.IsNullOrWhiteSpace(command.Password))
        {
            return OperationResult<SessionRecord>.Fail("Username and password are required.");
        }

        var valid = await _gateway.ValidateCredentialsAsync(command.Username, command.Password, ct);
        if (!valid)
        {
            await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Username, "Login", command.Username, false, "Invalid credentials"), ct);
            return OperationResult<SessionRecord>.Fail("Invalid username or password.");
        }

        var user = await _gateway.GetUserAsync(command.Username, "domain", ct);
        var displayName = user?.DisplayName ?? command.Username;
        var groups = user?.Groups ?? Array.Empty<string>();

        var session = await _sessions.CreateAsync(command.Username, displayName, groups, ct);

        await _audit.LogAsync(new AuditEntry(DateTimeOffset.UtcNow, command.Username, "Login", command.Username, true, null), ct);

        return OperationResult<SessionRecord>.Ok(session);
    }
}
