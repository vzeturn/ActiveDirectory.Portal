using System.Reflection;
using Sentry;
using Sentry.AspNetCore;

namespace ADManager.Api.Observability;

/// <summary>Settings bound from the <c>Sentry</c> configuration section.</summary>
public sealed class SentrySettings
{
    public const string SectionName = "Sentry";

    /// <summary>Project DSN; empty disables Sentry. Read from config or the SENTRY_DSN variable, never hardcoded.</summary>
    public string Dsn { get; init; } = string.Empty;

    public string Environment { get; init; } = "production";

    /// <summary>Share of requests traced for performance (kept low to stay inside the free plan).</summary>
    public double TracesSampleRate { get; init; } = 0.05;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(Dsn);
}

public static class SentrySetup
{
    public static SentrySettings ReadSettings(IConfiguration configuration)
    {
        var settings = configuration.GetSection(SentrySettings.SectionName).Get<SentrySettings>() ?? new SentrySettings();
        if (!settings.IsEnabled && System.Environment.GetEnvironmentVariable("SENTRY_DSN") is { Length: > 0 } envDsn)
        {
            settings = new SentrySettings
            {
                Dsn = envDsn,
                Environment = settings.Environment,
                TracesSampleRate = settings.TracesSampleRate,
            };
        }

        return settings;
    }

    /// <summary>Wires the Sentry SDK when a DSN is configured; a no-op otherwise.</summary>
    public static void AddSentry(WebApplicationBuilder builder)
    {
        var settings = ReadSettings(builder.Configuration);
        builder.Services.AddSingleton(settings);
        if (!settings.IsEnabled)
        {
            return;
        }

        builder.WebHost.UseSentry(options => Configure(options, settings));
    }

    public static void Configure(SentryAspNetCoreOptions options, SentrySettings settings)
    {
        options.Dsn = settings.Dsn;
        options.Environment = settings.Environment;
        options.Release = "ad-manager@" + (typeof(SentrySetup).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown");
        options.TracesSampleRate = Math.Clamp(settings.TracesSampleRate, 0, 1);

        // Privacy: no IPs, cookies, headers, bodies or user identity; scrub what is left.
        options.SendDefaultPii = false;
        options.MaxRequestBodySize = Sentry.Extensibility.RequestSize.None;
        options.CaptureFailedRequests = false;
        // Outgoing HTTP calls carry secret URLs (RT alert hook), so they are not recorded.
        options.DisableSentryHttpMessageHandler = true;
        options.MinimumBreadcrumbLevel = LogLevel.Warning;
        options.MinimumEventLevel = LogLevel.Error;
        options.SetBeforeSend((evt, _) => SentryPrivacy.Scrub(evt));
        options.SetBeforeSendTransaction((transaction, _) => SentryPrivacy.Scrub(transaction));
        options.SetBeforeBreadcrumb((breadcrumb, _) => SentryPrivacy.Scrub(breadcrumb));
    }
}
