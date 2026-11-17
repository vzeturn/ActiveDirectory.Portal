using System.Net.Http.Json;
using ADManager.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace ADManager.Infrastructure.Notifications;

public sealed class DiscordNotificationSender : INotificationSender
{
    private readonly HttpClient _http;
    private readonly ISecretsProvider _secrets;
    private readonly ILogger<DiscordNotificationSender> _logger;

    public DiscordNotificationSender(
        HttpClient http,
        ISecretsProvider secrets,
        ILogger<DiscordNotificationSender> logger)
    {
        _http = http;
        _secrets = secrets;
        _logger = logger;
    }

    public async Task NotifyAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            var url = await _secrets.GetDiscordWebhookUrlAsync(ct);
            if (string.IsNullOrWhiteSpace(url))
            {
                _logger.LogDebug("Discord webhook URL not configured; skipping notification.");
                return;
            }

            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = message.Title,
                        description = message.Description,
                        color = message.Success ? 3066993 : 15158332,
                        fields = message.Fields
                            .Select(f => new { name = f.Key, value = f.Value, inline = true })
                            .ToArray(),
                        timestamp = message.OccurredAt.ToString("o"),
                        footer = new { text = "AD Manager" }
                    }
                }
            };

            using var response = await _http.PostAsJsonAsync(url, payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Discord webhook returned {StatusCode} for notification {Title}.",
                    (int)response.StatusCode, message.Title);
            }
        }
        catch (Exception ex)
        {
            // Notification failures must never break the AD operation flow.
            _logger.LogWarning(ex, "Failed to send Discord notification {Title}.", message.Title);
        }
    }
}
