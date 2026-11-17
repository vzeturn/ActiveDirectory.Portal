using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace ADManager.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected string Actor =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";

    protected Guid SessionId =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    protected static string NormalizeScope(string? scope) =>
        string.Equals(scope, "domain", StringComparison.OrdinalIgnoreCase) ? "domain" : "ou";
}
