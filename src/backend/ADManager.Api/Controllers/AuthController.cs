using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.Abstractions;
using ADManager.Application.UseCases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ADManager.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController : ApiControllerBase
{
    private readonly LoginUseCase _login;
    private readonly ISessionService _sessions;
    private readonly IAdminGroupPolicy _adminPolicy;

    public AuthController(LoginUseCase login, ISessionService sessions, IAdminGroupPolicy adminPolicy)
    {
        _login = login;
        _sessions = sessions;
        _adminPolicy = adminPolicy;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _login.ExecuteAsync(
            new LoginCommand(request.Username, request.Password), ct);

        if (!result.Success || result.Value is null)
        {
            return Unauthorized(ApiResponse<UserInfoDto>.Fail(result.Error ?? "Invalid username or password."));
        }

        var session = result.Value;

        if (!_adminPolicy.IsAdmin(session.Groups) && !_adminPolicy.IsViewer(session.Groups))
        {
            await _sessions.RemoveAsync(session.Id, ct);
            return Unauthorized(ApiResponse<UserInfoDto>.Fail("Invalid username or password."));
        }

        Response.Cookies.Append(
            SessionAuthConstants.CookieName,
            session.Id.ToString(),
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = session.ExpiresAt
            });

        return Ok(ApiResponse<UserInfoDto>.Ok(UserInfoDto.From(session, _adminPolicy)));
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = SessionAuthConstants.Scheme)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await _sessions.RemoveAsync(SessionId, ct);
        Response.Cookies.Delete(SessionAuthConstants.CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });
        return Ok(ApiMessageResponse.Ok("Logged out."));
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = SessionAuthConstants.Scheme)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var session = await _sessions.GetAsync(SessionId, ct);
        if (session is null)
        {
            return Unauthorized(ApiResponse<UserInfoDto>.Fail("Session expired."));
        }

        return Ok(ApiResponse<UserInfoDto>.Ok(UserInfoDto.From(session, _adminPolicy)));
    }
}
