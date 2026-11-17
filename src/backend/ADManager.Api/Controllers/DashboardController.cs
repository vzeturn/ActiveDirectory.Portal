using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ADManager.Api.Controllers;

[Route("api/dashboard")]
[Authorize(AuthenticationSchemes = SessionAuthConstants.AnyScheme, Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
public sealed class DashboardController : ApiControllerBase
{
    private readonly DashboardSummaryUseCase _summary;

    public DashboardController(DashboardSummaryUseCase summary)
    {
        _summary = summary;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> Summary([FromQuery] string? scope, CancellationToken ct)
    {
        var result = await _summary.ExecuteAsync(NormalizeScope(scope), Actor, ct);
        return result.Success
            ? Ok(ApiResponse<DashboardSummary>.Ok(result.Value!))
            : BadRequest(ApiResponse<DashboardSummary>.Fail(result.Error!));
    }
}
