namespace ADManager.Domain;

public sealed record SessionRecord(
    Guid Id,
    string Username,
    string DisplayName,
    IReadOnlyList<string> Groups,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
