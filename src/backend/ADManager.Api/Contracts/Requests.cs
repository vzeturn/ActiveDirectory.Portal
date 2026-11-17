using System.ComponentModel.DataAnnotations;

namespace ADManager.Api.Contracts;

public sealed class LoginRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Username { get; set; } = string.Empty;

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ResetPasswordRequest
{
    [StringLength(128)]
    public string? NewPassword { get; set; }

    public bool RequireChange { get; set; } = false;
}

public sealed class LdapsResetPasswordRequest
{
    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class GroupMembershipRequest
{
    [Required]
    [StringLength(1024, MinimumLength = 1)]
    public string GroupNames { get; set; } = string.Empty;

    public IReadOnlyList<string> GetGroupNames() =>
        GroupNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
}

public sealed class AddMembersRequest
{
    [Required]
    [StringLength(1024, MinimumLength = 1)]
    public string MemberNames { get; set; } = string.Empty;

    public IReadOnlyList<string> GetMemberNames() =>
        MemberNames
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
}

public sealed class CreateGroupRequest
{
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1024)]
    public string? Description { get; set; }
}

public sealed class UpdateGroupDescriptionRequest
{
    [StringLength(1024)]
    public string? Description { get; set; }
}

public sealed class BulkUsernamesRequest
{
    [Required]
    [MinLength(1)]
    public string[] Usernames { get; set; } = Array.Empty<string>();
}

public sealed class BulkGroupAssignmentRequest
{
    [Required]
    [MinLength(1)]
    public string[] Usernames { get; set; } = Array.Empty<string>();

    [Required]
    [MinLength(1)]
    public string[] GroupNames { get; set; } = Array.Empty<string>();
}

public sealed class BulkResetPasswordRequest
{
    [Required]
    [MinLength(1)]
    public string[] Usernames { get; set; } = Array.Empty<string>();

    public bool RequireChange { get; set; } = true;
}
