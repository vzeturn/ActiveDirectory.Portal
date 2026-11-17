using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADManager.Api.Controllers;

[Route("api/users")]
[Authorize(AuthenticationSchemes = SessionAuthConstants.AnyScheme)]
public sealed class UsersController : ApiControllerBase
{
    private readonly LookupUserUseCase _lookup;
    private readonly ToggleAccountUseCase _toggle;
    private readonly ResetPasswordUseCase _reset;
    private readonly LdapsResetPasswordUseCase _resetLdaps;
    private readonly ManageGroupMembershipUseCase _groups;
    private readonly BulkAccountOpUseCase _bulkAccount;
    private readonly BulkGroupAssignmentUseCase _bulkGroups;
    private readonly BulkResetPasswordUseCase _bulkReset;

    public UsersController(
        LookupUserUseCase lookup,
        ToggleAccountUseCase toggle,
        ResetPasswordUseCase reset,
        LdapsResetPasswordUseCase resetLdaps,
        ManageGroupMembershipUseCase groups,
        BulkAccountOpUseCase bulkAccount,
        BulkGroupAssignmentUseCase bulkGroups,
        BulkResetPasswordUseCase bulkReset)
    {
        _lookup = lookup;
        _toggle = toggle;
        _reset = reset;
        _resetLdaps = resetLdaps;
        _groups = groups;
        _bulkAccount = bulkAccount;
        _bulkGroups = bulkGroups;
        _bulkReset = bulkReset;
    }

    [HttpGet("search")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 100,
        [FromQuery] string? scope = null,
        [FromQuery] string? ou = null,
        [FromQuery] bool? enabledOnly = null,
        [FromQuery] bool? lockedOnly = null,
        [FromQuery] string? memberOf = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 100)
        {
            return BadRequest(ApiResponse<IReadOnlyList<AdUser>>.Fail("Search term is required and must be 100 characters or fewer."));
        }

        if (limit is < 1 or > 1000)
        {
            return BadRequest(ApiResponse<IReadOnlyList<AdUser>>.Fail("Limit must be between 1 and 1000."));
        }

        var result = await _lookup.SearchAsync(
            new SearchUsersQuery(q, limit, NormalizeScope(scope), Actor, ou, enabledOnly, lockedOnly, memberOf), ct);

        return result.Success
            ? Ok(ApiResponse<IReadOnlyList<AdUser>>.Ok(result.Value ?? Array.Empty<AdUser>()))
            : BadRequest(ApiResponse<IReadOnlyList<AdUser>>.Fail(result.Error!));
    }

    [HttpGet("{username}")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Get(
        string username,
        [FromQuery] string? scope = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 64)
        {
            return BadRequest(ApiResponse<AdUser>.Fail("Username is required and must be 64 characters or fewer."));
        }

        var result = await _lookup.GetAsync(new GetUserQuery(username, NormalizeScope(scope), Actor), ct);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<AdUser>.Fail(result.Error!));
        }

        return result.Value is null
            ? NotFound(ApiResponse<AdUser>.Fail($"User '{username}' was not found."))
            : Ok(ApiResponse<AdUser>.Ok(result.Value));
    }

    [HttpPut("{username}/enable")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> Enable(string username, [FromQuery] string? scope, CancellationToken ct) =>
        RunAccountOp(() => _toggle.EnableAsync(new AccountCommand(username, NormalizeScope(scope), Actor), ct));

    [HttpPut("{username}/disable")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> Disable(string username, [FromQuery] string? scope, CancellationToken ct) =>
        RunAccountOp(() => _toggle.DisableAsync(new AccountCommand(username, NormalizeScope(scope), Actor), ct));

    [HttpPut("{username}/unlock")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> Unlock(string username, [FromQuery] string? scope, CancellationToken ct) =>
        RunAccountOp(() => _toggle.UnlockAsync(new AccountCommand(username, NormalizeScope(scope), Actor), ct));

    [HttpPost("{username}/reset-password")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> ResetPassword(
        string username,
        [FromBody] ResetPasswordRequest request,
        [FromQuery] string? scope,
        CancellationToken ct)
    {
        var result = await _reset.ExecuteAsync(
            new ResetPasswordCommand(username, request.NewPassword, request.RequireChange, NormalizeScope(scope), Actor),
            ct);

        return result.Success
            ? Ok(ApiResponse<string>.Ok(result.Value!))
            : BadRequest(ApiResponse<string>.Fail(result.Error!));
    }

    [HttpPost("{username}/reset-password-ldaps")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> ResetPasswordLdaps(
        string username,
        [FromBody] LdapsResetPasswordRequest request,
        [FromQuery] string? scope,
        CancellationToken ct)
    {
        var result = await _resetLdaps.ExecuteAsync(
            new LdapsResetPasswordCommand(username, request.NewPassword, NormalizeScope(scope), Actor),
            ct);

        return result.Success
            ? Ok(ApiMessageResponse.Ok($"Password reset via LDAPS for '{result.Value}'."))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpGet("{username}/groups")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> GetGroups(string username, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _groups.GetMembershipAsync(
            new GroupMembershipQuery(username, NormalizeScope(scope), Actor), ct);

        return result.Success
            ? Ok(ApiResponse<IReadOnlyList<string>>.Ok(result.Value ?? Array.Empty<string>()))
            : BadRequest(ApiResponse<IReadOnlyList<string>>.Fail(result.Error!));
    }

    [HttpPost("{username}/groups")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> AddToGroups(
        string username,
        [FromBody] GroupMembershipRequest request,
        [FromQuery] string? scope,
        CancellationToken ct)
    {
        var result = await _groups.AddAsync(
            new ChangeGroupMembershipCommand(username, request.GetGroupNames(), NormalizeScope(scope), Actor), ct);

        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpDelete("{username}/groups/{groupName}")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> RemoveFromGroup(
        string username,
        string groupName,
        [FromQuery] string? scope,
        CancellationToken ct)
    {
        var result = await _groups.RemoveAsync(
            new ChangeGroupMembershipCommand(username, new[] { groupName }, NormalizeScope(scope), Actor), ct);

        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpPost("bulk/enable")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> BulkEnable([FromBody] BulkUsernamesRequest request, [FromQuery] string? scope, CancellationToken ct) =>
        RunBulkOp(_bulkAccount.ExecuteAsync(new BulkAccountOpCommand(request.Usernames, "enable", NormalizeScope(scope), Actor), ct));

    [HttpPost("bulk/disable")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> BulkDisable([FromBody] BulkUsernamesRequest request, [FromQuery] string? scope, CancellationToken ct) =>
        RunBulkOp(_bulkAccount.ExecuteAsync(new BulkAccountOpCommand(request.Usernames, "disable", NormalizeScope(scope), Actor), ct));

    [HttpPost("bulk/unlock")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> BulkUnlock([FromBody] BulkUsernamesRequest request, [FromQuery] string? scope, CancellationToken ct) =>
        RunBulkOp(_bulkAccount.ExecuteAsync(new BulkAccountOpCommand(request.Usernames, "unlock", NormalizeScope(scope), Actor), ct));

    [HttpPost("bulk/groups")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> BulkAddToGroups([FromBody] BulkGroupAssignmentRequest request, [FromQuery] string? scope, CancellationToken ct) =>
        RunBulkOp(_bulkGroups.ExecuteAsync(new BulkGroupAssignmentCommand(request.Usernames, request.GroupNames, NormalizeScope(scope), Actor), ct));

    [HttpPost("bulk/reset-password")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public Task<IActionResult> BulkResetPassword([FromBody] BulkResetPasswordRequest request, [FromQuery] string? scope, CancellationToken ct) =>
        RunBulkOp(_bulkReset.ExecuteAsync(new BulkResetPasswordCommand(request.Usernames, request.RequireChange, NormalizeScope(scope), Actor), ct));

    private static async Task<IActionResult> RunBulkOp(Task<OperationResult<IReadOnlyList<BulkOpItemResult>>> op)
    {
        var result = await op;
        return result.Success
            ? new OkObjectResult(ApiResponse<IReadOnlyList<BulkOpItemResult>>.Ok(result.Value!))
            : new BadRequestObjectResult(ApiResponse<IReadOnlyList<BulkOpItemResult>>.Fail(result.Error!));
    }

    private static async Task<IActionResult> RunAccountOp(Func<Task<OperationResult<string>>> op)
    {
        var result = await op();
        return result.Success
            ? new OkObjectResult(ApiMessageResponse.Ok(result.Value!))
            : new BadRequestObjectResult(ApiMessageResponse.Fail(result.Error!));
    }
}
