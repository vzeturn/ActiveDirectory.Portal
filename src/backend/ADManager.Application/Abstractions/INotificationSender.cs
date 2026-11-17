namespace ADManager.Application.Abstractions;

public interface INotificationSender
{
    Task NotifyAsync(NotificationMessage message, CancellationToken ct = default);
}

public sealed record NotificationMessage(
    string Title,
    string Description,
    bool Success,
    IReadOnlyDictionary<string, string> Fields,
    DateTimeOffset OccurredAt);
