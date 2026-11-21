using System.Net.Http.Json;
using System.Text;
using ADManager.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ADManager.Infrastructure.Notifications;

/// <summary>
/// Sends operational alerts to the Request Tracking hub alert hook. Info alerts are only
/// logged; Warning and Critical alerts are posted as JSON {severity, title, text, tenant}
/// and the hub opens an incident request for each one.
/// </summary>
public sealed class RtAlertHookSender : INotificationSender
{
    private readonly HttpClient _http;
    private readonly ISecretsProvider _secrets;
    private readonly ILogger<RtAlertHookSender> _logger;
    private readonly string _tenant;

    public RtAlertHookSender(
        HttpClient http,
        ISecretsProvider secrets,
        IOptions<AlertSettings> settings,
        ILogger<RtAlertHookSender> logger)
    {
        _http = http;
        _secrets = secrets;
        _logger = logger;
        _tenant = string.IsNullOrWhiteSpace(settings.Value.Tenant)
            ? Environment.MachineName
            : settings.Value.Tenant.Trim();
    }

    public async Task NotifyAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            await SendAsync(message, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Alert delivery failures must never break the AD operation flow; the failure is
            // logged with the alert title so it can still be traced from the server logs.
            _logger.LogWarning(ex, "Failed to send {Severity} alert {Title} to the RT hub.", message.Severity, message.Title);
        }
    }

    /// <summary>Delivers one alert and reports what happened (used by the send-test-alert CLI).</summary>
    public async Task<AlertDeliveryResult> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        var text = BuildText(message);
        _logger.LogInformation(
            "Alert {Severity}: {Title} | {Text} | tenant {Tenant}",
            message.Severity, message.Title, text, _tenant);

        if (message.Severity == AlertSeverity.Info)
        {
            return AlertDeliveryResult.LoggedOnly;
        }

        var url = await _secrets.GetAlertHookUrlAsync(ct);
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning(
                "RT alert hook URL is not configured (Secrets:AlertHookUrlEncrypted); {Severity} alert {Title} was only logged.",
                message.Severity, message.Title);
            return AlertDeliveryResult.NotConfigured;
        }

        var payload = new
        {
            severity = message.Severity.ToString().ToLowerInvariant(),
            title = message.Title,
            text,
            tenant = _tenant
        };

        using var response = await _http.PostAsJsonAsync(url, payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "RT alert hook returned {StatusCode} for {Severity} alert {Title}.",
                (int)response.StatusCode, message.Severity, message.Title);
            return AlertDeliveryResult.Rejected((int)response.StatusCode);
        }

        return AlertDeliveryResult.Delivered((int)response.StatusCode);
    }

    internal static string BuildText(NotificationMessage message)
    {
        var sb = new StringBuilder(message.Description);
        foreach (var (key, value) in message.Fields)
        {
            // Defence in depth: nothing password-like ever leaves the server.
            if (key.Contains("password", StringComparison.OrdinalIgnoreCase)) continue;
            sb.Append('\n').Append(key).Append(": ").Append(value);
        }
        sb.Append("\nAt: ").Append(message.OccurredAt.ToString("o"));
        return sb.ToString();
    }
}

public sealed record AlertDeliveryResult(string Outcome, int? StatusCode)
{
    public static readonly AlertDeliveryResult LoggedOnly = new("logged-only", null);
    public static readonly AlertDeliveryResult NotConfigured = new("not-configured", null);
    public static AlertDeliveryResult Delivered(int status) => new("delivered", status);
    public static AlertDeliveryResult Rejected(int status) => new("rejected", status);

    public bool IsDelivered => Outcome == "delivered";
}
