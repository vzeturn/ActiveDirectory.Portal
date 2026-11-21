namespace ADManager.Application.Abstractions;

public interface INotificationSender
{
    Task NotifyAsync(NotificationMessage message, CancellationToken ct = default);
}

/// <summary>
/// Operational alert level. Info is only logged; Warning and Critical are sent to the
/// Request Tracking hub, which opens an incident request for each one.
/// </summary>
public enum AlertSeverity
{
    Info,
    Warning,
    Critical
}

/// <remarks>Fields leave the server (logs, hub incidents): never put secrets such as passwords in them.</remarks>
public sealed record NotificationMessage(
    string Title,
    string Description,
    AlertSeverity Severity,
    IReadOnlyDictionary<string, string> Fields,
    DateTimeOffset OccurredAt);
