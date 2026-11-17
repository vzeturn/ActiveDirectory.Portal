namespace ADManager.Domain;

public sealed record SavedSearch(string Id, string Owner, string Name, string FilterJson, DateTimeOffset CreatedAt);
