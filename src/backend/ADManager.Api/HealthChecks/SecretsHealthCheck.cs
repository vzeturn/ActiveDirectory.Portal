using ADManager.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ADManager.Api.HealthChecks;

public sealed class SecretsHealthCheck : IHealthCheck
{
    private readonly ISecretsProvider _secrets;

    public SecretsHealthCheck(ISecretsProvider secrets)
    {
        _secrets = secrets;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var creds = await _secrets.GetServiceAccountAsync(cancellationToken);
            return string.IsNullOrWhiteSpace(creds.Username)
                ? HealthCheckResult.Unhealthy("Secrets configuration returned an empty service account.")
                : HealthCheckResult.Healthy("Secrets configuration loaded.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Secrets configuration unreadable.", ex);
        }
    }
}
