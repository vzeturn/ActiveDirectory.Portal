namespace ADManager.Domain;

public sealed record AuditEntry(
    DateTimeOffset Timestamp,
    string Actor,
    string Operation,
    string Target,
    bool Result,
    string? Details);
