using System.Security.Claims;
using System.Text.Encodings.Web;
using ADManager.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADManager.Api.Auth;

public sealed class SessionCookieAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISessionService _sessions;
    private readonly IAdminGroupPolicy _adminPolicy;

    public SessionCookieAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionService sessions,
        IAdminGroupPolicy adminPolicy)
        : base(options, logger, encoder)
    {
        _sessions = sessions;
        _adminPolicy = adminPolicy;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Cookies.TryGetValue(SessionAuthConstants.CookieName, out var raw)
            || string.IsNullOrWhiteSpace(raw))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Guid.TryParse(raw, out var sessionId))
        {
            return AuthenticateResult.Fail("Malformed session cookie.");
        }

        var session = await _sessions.GetAsync(sessionId, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Session expired or not found.");
        }

        await _sessions.TouchAsync(sessionId, Context.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.Id.ToString()),
            new(ClaimTypes.Name, session.Username),
            new("displayName", session.DisplayName)
        };

        foreach (var group in session.Groups)
        {
            claims.Add(new Claim("adGroup", group));
        }

        if (_adminPolicy.IsAdmin(session.Groups))
        {
            claims.Add(new Claim(ClaimTypes.Role, SessionAuthConstants.AdminRole));
        }

        if (_adminPolicy.IsViewer(session.Groups))
        {
            claims.Add(new Claim(ClaimTypes.Role, SessionAuthConstants.ViewerRole));
        }

        var identity = new ClaimsIdentity(claims, SessionAuthConstants.Scheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionAuthConstants.Scheme);
        return AuthenticateResult.Success(ticket);
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
