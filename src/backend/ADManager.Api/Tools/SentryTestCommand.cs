using ADManager.Api.Observability;
using Sentry;

namespace ADManager.Api.Tools;

/// <summary>Sends one test error to Sentry and prints the event id, proving the server can report errors.</summary>
public static class SentryTestCommand
{
    public static async Task<int> RunAsync(IServiceProvider services)
    {
        var settings = services.GetRequiredService<SentrySettings>();
        if (!settings.IsEnabled)
        {
            Console.Error.WriteLine("Sentry is not configured: set Sentry:Dsn in appsettings.Production.json (deploy.ps1 -SentryDsn).");
            return 2;
        }

        // Resolving the hub initialises the SDK registered by UseSentry.
        var hub = services.GetRequiredService<IHub>();
        if (!hub.IsEnabled)
        {
            Console.Error.WriteLine("Sentry SDK did not initialise; check the DSN.");
            return 1;
        }

        var eventId = hub.CaptureException(new SentryTestException(
            "AD Manager test error sent by the send-test-error operator command."));
        await hub.FlushAsync(TimeSpan.FromSeconds(10));
        Console.WriteLine($"Sentry test error sent: event {eventId} (environment {settings.Environment}).");
        return 0;
    }
}

public sealed class SentryTestException(string message) : Exception(message);
