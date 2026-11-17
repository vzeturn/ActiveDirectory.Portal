using ADManager.Api.Auth;
using ADManager.Api.Contracts;
using ADManager.Application.Abstractions;
using ADManager.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace ADManager.Api.Controllers;

[Route("api/audit")]
[Authorize(AuthenticationSchemes = SessionAuthConstants.AnyScheme)]
public sealed class AuditController : ApiControllerBase
{
    private readonly IAuditLogger _audit;

    public AuditController(IAuditLogger audit)
    {
        _audit = audit;
    }

    [HttpGet]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Query(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? actor,
        [FromQuery] string? operation,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 500) pageSize = 50;

        var entries = await _audit.QueryAsync(
            new AuditQuery(from, to, actor, operation, page, pageSize), ct);

        return Ok(ApiResponse<IReadOnlyList<AuditEntry>>.Ok(entries));
    }

    [HttpGet("export")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> Export(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? actor,
        [FromQuery] string? operation,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 500,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize is < 1 or > 500) pageSize = 500;

        var entries = await _audit.QueryAsync(new AuditQuery(from, to, actor, operation, page, pageSize), ct);

        var csv = new StringBuilder();
        csv.AppendLine("Timestamp,Actor,Operation,Target,Result,Details");
        foreach (var e in entries)
        {
            csv.AppendLine(string.Join(',',
                e.Timestamp.ToString("o"),
                CsvEscape(e.Actor),
                CsvEscape(e.Operation),
                CsvEscape(e.Target),
                e.Result.ToString(),
                CsvEscape(e.Details ?? string.Empty)));
        }

        var bytes = Encoding.UTF8.GetBytes(csv.ToString());
        return File(bytes, "text/csv", $"audit-log-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
    }

    [HttpGet("operations")]
    [Authorize(Roles = $"{SessionAuthConstants.AdminRole},{SessionAuthConstants.ViewerRole}")]
    public async Task<IActionResult> DistinctOperations(CancellationToken ct = default)
    {
        var entries = await _audit.QueryAsync(new AuditQuery(null, null, null, null, 1, 500), ct);
        var operations = entries.Select(e => e.Operation).Distinct().OrderBy(o => o).ToList();
        return Ok(ApiResponse<IReadOnlyList<string>>.Ok(operations));
    }

    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    private static string CsvEscape(string value)
    {
        if (value.Length > 0 && FormulaTriggers.Contains(value[0]))
        {
            value = "'" + value;
        }

        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
