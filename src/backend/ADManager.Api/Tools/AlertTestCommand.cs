using ADManager.Application.Abstractions;
using ADManager.Infrastructure.Notifications;

namespace ADManager.Api.Tools;

/// <summary>Sends one warning alert through the RT alert hook and prints the outcome.</summary>
public static class AlertTestCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<INotificationSender>() is not RtAlertHookSender sender)
        {
            Console.Error.WriteLine("The registered notification sender is not the RT alert hook sender.");
            return 2;
        }

        var message = new NotificationMessage(
            "AD Manager test alert",
            "Test warning sent by the operator command 'send-test-alert'. Close this incident once seen.",
            AlertSeverity.Warning,
            new Dictionary<string, string> { ["Source"] = "send-test-alert" },
            DateTimeOffset.UtcNow);

        try
        {
            var result = await sender.SendAsync(message);
            Console.WriteLine($"Alert outcome: {result.Outcome}{(result.StatusCode is { } code ? $" (HTTP {code})" : string.Empty)}");
            return result.IsDelivered ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Alert failed: {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
