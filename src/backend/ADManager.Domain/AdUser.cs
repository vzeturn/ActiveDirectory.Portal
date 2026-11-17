namespace ADManager.Domain;

public sealed record AdUser
{
    public required string Username { get; init; }
    public string? DisplayName { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? Title { get; init; }
    public string? Department { get; init; }
    public string? OrganizationalUnit { get; init; }
    public string? Company { get; init; }
    public string? Manager { get; init; }
    public bool Enabled { get; init; }
    public bool IsLockedOut { get; init; }
    public string? DistinguishedName { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Modified { get; init; }
    public DateTime? LastLogon { get; init; }
    public DateTime? PasswordLastSet { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
}
