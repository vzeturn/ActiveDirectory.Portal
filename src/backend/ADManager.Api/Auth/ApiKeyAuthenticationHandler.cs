using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using ADManager.Application.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADManager.Api.Auth;

public sealed class ApiKeyAuthenticationHandler
    : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly ISecretsProvider _secrets;

    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISecretsProvider secrets)
        : base(options, logger, encoder)
    {
        _secrets = secrets;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(SessionAuthConstants.ApiKeyHeaderName, out var provided)
            || string.IsNullOrWhiteSpace(provided))
        {
            return AuthenticateResult.NoResult();
        }

        var configured = await _secrets.GetApiKeyAsync(Context.RequestAborted);
        if (string.IsNullOrWhiteSpace(configured) || !FixedTimeEquals(provided.ToString(), configured))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var owner = await _secrets.GetApiKeyOwnerAsync(Context.RequestAborted) ?? "api-key";
        var configuredRole = await _secrets.GetApiKeyRoleAsync(Context.RequestAborted);
        var role = configuredRole switch
        {
            SessionAuthConstants.AdminRole => SessionAuthConstants.AdminRole,
            SessionAuthConstants.ViewerRole => SessionAuthConstants.ViewerRole,
            null => SessionAuthConstants.ViewerRole,
            _ => LogInvalidRoleAndFallBack(configuredRole)
        };
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, owner),
            new(ClaimTypes.Name, owner),
            new("displayName", $"{owner} (API key)"),
            new(ClaimTypes.Role, role)
        };

        var identity = new ClaimsIdentity(claims, SessionAuthConstants.ApiKeyScheme);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SessionAuthConstants.ApiKeyScheme);
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

    private static bool FixedTimeEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }

    private string LogInvalidRoleAndFallBack(string configuredRole)
    {
        Logger.LogWarning(
            "Configured ApiKeyRole '{ConfiguredRole}' is not a recognized role ({AdminRole}/{ViewerRole}); falling back to {ViewerRole}.",
            configuredRole, SessionAuthConstants.AdminRole, SessionAuthConstants.ViewerRole, SessionAuthConstants.ViewerRole);
        return SessionAuthConstants.ViewerRole;
    }
}
