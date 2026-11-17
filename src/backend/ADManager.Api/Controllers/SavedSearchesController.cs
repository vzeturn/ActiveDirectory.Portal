using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.Abstractions;
using ADManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADManager.Api.Controllers;

[Route("api/saved-searches")]
[Authorize(AuthenticationSchemes = SessionAuthConstants.AnyScheme)]
public sealed class SavedSearchesController : ApiControllerBase
{
    private readonly ISavedSearchRepository _repository;

    public SavedSearchesController(ISavedSearchRepository repository)
    {
        _repository = repository;
    }

    public sealed record CreateSavedSearchRequest(string Name, string FilterJson);

    [HttpGet]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var results = await _repository.ListByOwnerAsync(Actor, ct);
        return Ok(ApiResponse<IReadOnlyList<SavedSearch>>.Ok(results));
    }

    [HttpPost]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Create([FromBody] CreateSavedSearchRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100)
        {
            return BadRequest(ApiResponse<SavedSearch>.Fail("Name is required and must be 100 characters or fewer."));
        }

        if (string.IsNullOrWhiteSpace(request.FilterJson) || request.FilterJson.Length > 4096)
        {
            return BadRequest(ApiResponse<SavedSearch>.Fail("FilterJson is required and must be 4096 characters or fewer."));
        }

        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(request.FilterJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(ApiResponse<SavedSearch>.Fail("FilterJson must be valid JSON."));
        }

        var search = new SavedSearch(Guid.NewGuid().ToString(), Actor, request.Name, request.FilterJson, DateTimeOffset.UtcNow);
        await _repository.CreateAsync(search, ct);
        return Ok(ApiResponse<SavedSearch>.Ok(search));
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        await _repository.DeleteAsync(id, Actor, ct);
        return Ok(ApiMessageResponse.Ok("Saved search deleted."));
    }
}
