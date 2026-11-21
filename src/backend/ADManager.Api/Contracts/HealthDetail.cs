namespace ADManager.Api.Contracts;

public sealed record HealthDetail(
    bool AdReachable,
    bool SecretsLoaded,
    bool AuditDbWritable,
    bool AlertHookConfigured);
