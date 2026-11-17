using ADManager.Application.Abstractions;
using ADManager.Domain;

namespace ADManager.Api.Contracts;

public sealed class UserInfoDto
{
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public IReadOnlyList<string> Groups { get; init; } = Array.Empty<string>();
    public bool IsAdmin { get; init; }
    public bool IsViewer { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    public static UserInfoDto From(SessionRecord session, IAdminGroupPolicy policy) => new()
    {
        Username = session.Username,
        DisplayName = session.DisplayName,
        Groups = session.Groups,
        IsAdmin = policy.IsAdmin(session.Groups),
        IsViewer = policy.IsViewer(session.Groups),
        ExpiresAt = session.ExpiresAt
    };
}
