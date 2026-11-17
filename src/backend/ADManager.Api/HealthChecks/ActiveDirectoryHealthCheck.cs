using ADManager.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ADManager.Api.HealthChecks;

public sealed class ActiveDirectoryHealthCheck : IHealthCheck
{
    private readonly IActiveDirectoryGateway _gateway;

    public ActiveDirectoryHealthCheck(IActiveDirectoryGateway gateway)
    {
        _gateway = gateway;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var ok = await _gateway.CanBindAsync(cancellationToken);
            return ok
                ? HealthCheckResult.Healthy("Active Directory service-account bind succeeded.")
                : HealthCheckResult.Unhealthy("Active Directory service-account bind failed.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Active Directory health check threw.", ex);
        }
    }
}
