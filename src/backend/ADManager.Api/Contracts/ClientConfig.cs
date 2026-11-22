namespace ADManager.Api.Contracts;

/// <summary>Browser settings served anonymously by GET /api/client-config.</summary>
public sealed record ClientConfig(string? SentryDsn, string SentryEnvironment, double SentryTracesSampleRate);
