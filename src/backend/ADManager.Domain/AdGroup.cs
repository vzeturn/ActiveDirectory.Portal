namespace ADManager.Domain;

public sealed record AdGroupMember
{
    public required string Username { get; init; }
    public string? DisplayName { get; init; }
    public string? Email { get; init; }
}

public sealed record AdGroup
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? DistinguishedName { get; init; }
    public int MemberCount { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public IReadOnlyList<AdGroupMember> Members { get; init; } = Array.Empty<AdGroupMember>();
    public IReadOnlyList<string> ParentGroups { get; init; } = Array.Empty<string>();
}
