using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADManager.Api.Controllers;

[Route("api/groups")]
[Authorize(AuthenticationSchemes = SessionAuthConstants.AnyScheme)]
public sealed class GroupsController : ApiControllerBase
{
    private readonly LookupGroupUseCase _lookup;
    private readonly ManageGroupUseCase _manage;
    private readonly ManageGroupMembershipUseCase _membership;

    public GroupsController(
        LookupGroupUseCase lookup,
        ManageGroupUseCase manage,
        ManageGroupMembershipUseCase membership)
    {
        _lookup = lookup;
        _manage = manage;
        _membership = membership;
    }

    [HttpGet("search")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Search(
        [FromQuery] string q,
        [FromQuery] int limit = 100,
        [FromQuery] string? scope = null,
        [FromQuery] string? ou = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length > 100)
        {
            return BadRequest(ApiResponse<IReadOnlyList<AdGroup>>.Fail("Search term is required and must be 100 characters or fewer."));
        }

        if (limit is < 1 or > 1000)
        {
            return BadRequest(ApiResponse<IReadOnlyList<AdGroup>>.Fail("Limit must be between 1 and 1000."));
        }

        var result = await _lookup.SearchAsync(
            new SearchGroupsQuery(q, limit, NormalizeScope(scope), Actor, ou), ct);

        return result.Success
            ? Ok(ApiResponse<IReadOnlyList<AdGroup>>.Ok(result.Value ?? Array.Empty<AdGroup>()))
            : BadRequest(ApiResponse<IReadOnlyList<AdGroup>>.Fail(result.Error!));
    }

    [HttpGet("{groupName}")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Get(
        string groupName,
        [FromQuery] string? scope = null,
        CancellationToken ct = default)
    {
        var result = await _lookup.GetAsync(new GetGroupQuery(groupName, NormalizeScope(scope), Actor), ct);

        if (!result.Success)
        {
            return BadRequest(ApiResponse<AdGroup>.Fail(result.Error!));
        }

        return result.Value is null
            ? NotFound(ApiResponse<AdGroup>.Fail($"Group '{groupName}' was not found."))
            : Ok(ApiResponse<AdGroup>.Ok(result.Value));
    }

    [HttpPost]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> Create([FromBody] CreateGroupRequest request, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _manage.CreateAsync(new CreateGroupCommand(request.Name, request.Description, NormalizeScope(scope), Actor), ct);
        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpDelete("{groupName}")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> Delete(string groupName, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _manage.DeleteAsync(new DeleteGroupCommand(groupName, NormalizeScope(scope), Actor), ct);
        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpPut("{groupName}/description")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> UpdateDescription(string groupName, [FromBody] UpdateGroupDescriptionRequest request, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _manage.UpdateDescriptionAsync(new UpdateGroupDescriptionCommand(groupName, request.Description, NormalizeScope(scope), Actor), ct);
        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }

    [HttpPost("{groupName}/members")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> AddMembers(string groupName, [FromBody] AddMembersRequest request, [FromQuery] string? scope, CancellationToken ct)
    {
        var results = new List<BulkOpItemResult>();
        foreach (var username in request.GetMemberNames())
        {
            var result = await _membership.AddAsync(new ChangeGroupMembershipCommand(username, new[] { groupName }, NormalizeScope(scope), Actor), ct);
            results.Add(new BulkOpItemResult(username, result.Success, result.Success ? null : result.Error));
        }
        return Ok(ApiResponse<IReadOnlyList<BulkOpItemResult>>.Ok(results));
    }

    [HttpDelete("{groupName}/members/{username}")]
    [Authorize(Roles = SessionAuthConstants.AdminRole)]
    public async Task<IActionResult> RemoveMember(string groupName, string username, [FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _membership.RemoveAsync(new ChangeGroupMembershipCommand(username, new[] { groupName }, NormalizeScope(scope), Actor), ct);
        return result.Success
            ? Ok(ApiMessageResponse.Ok(result.Value!))
            : BadRequest(ApiMessageResponse.Fail(result.Error!));
    }
}
