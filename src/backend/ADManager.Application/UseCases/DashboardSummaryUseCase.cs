using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Application.UseCases;

public sealed record DashboardSummary(int LockedOutCount, int ExpiringSoonCount, IReadOnlyList<AuditEntry> RecentActivity);

public sealed class DashboardSummaryUseCase
{
    private readonly IActiveDirectoryGateway _gateway;
    private readonly IAuditLogger _audit;
    private readonly int _expiryWarningDays;

    public DashboardSummaryUseCase(IActiveDirectoryGateway gateway, IAuditLogger audit, int expiryWarningDays)
    {
        _gateway = gateway;
        _audit = audit;
        _expiryWarningDays = expiryWarningDays;
    }

    public async Task<OperationResult<DashboardSummary>> ExecuteAsync(string scope, string actor, CancellationToken ct = default)
    {
        try
        {
            // An empty query matches all users under the gateway's convention,
            // used here for a full scoped scan.
            var users = await _gateway.SearchUsersAsync(string.Empty, 1000, scope, includeGroups: false, ct);
            var cutoff = DateTime.UtcNow.AddDays(-_expiryWarningDays);

            var lockedOut = users.Count(u => u.IsLockedOut);
            var expiringSoon = users.Count(u => u.PasswordLastSet.HasValue && u.PasswordLastSet.Value <= cutoff);

            var recent = await _audit.QueryAsync(new AuditQuery(null, null, null, null, 1, 10), ct);

            return OperationResult<DashboardSummary>.Ok(new DashboardSummary(lockedOut, expiringSoon, recent));
        }
        catch (Exception ex)
        {
            return OperationResult<DashboardSummary>.Fail($"Failed to build dashboard summary: {ex.Message}");
        }
    }
}
