using System.Text.RegularExpressions;
using Sentry;

namespace ADManager.Api.Observability;

/// <summary>
/// Strips personal data from Sentry payloads before they leave the server. The portal works on
/// real directory accounts, so user names, distinguished names, e-mail addresses and request
/// details must never reach Sentry. Applied on top of <c>SendDefaultPii = false</c>.
/// </summary>
public static partial class SentryPrivacy
{
    public const string Redacted = "[redacted]";

    // Request headers that carry no personal data; every other header is dropped.
    private static readonly HashSet<string> SafeHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Accept", "Content-Type", "Content-Length", "Host",
    };

    [GeneratedRegex(@"\b(CN|OU|DC|UID|O|L|ST|C)=[^,;""'\r\n]+", RegexOptions.IgnoreCase)]
    private static partial Regex DistinguishedNamePart();

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailOrUpn();

    [GeneratedRegex(@"\b[A-Za-z0-9\-]{1,15}\\[A-Za-z0-9._$\-]+")]
    private static partial Regex DownLevelLogon();

    [GeneratedRegex(@"(/api/(?:users|groups|saved-searches))/[^/\s?#""']+(?:/(members|groups)/[^/\s?#""']+)?", RegexOptions.IgnoreCase)]
    private static partial Regex ResourcePath();

    [GeneratedRegex(@"'[^']*'|""[^""]*""")]
    private static partial Regex QuotedValue();

    /// <summary>Replaces directory identifiers and quoted values in free text.</summary>
    public static string? Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = ResourcePath().Replace(text, m =>
            m.Groups[2].Success ? $"{m.Groups[1].Value}/{Redacted}/{m.Groups[2].Value}/{Redacted}" : $"{m.Groups[1].Value}/{Redacted}");
        result = DistinguishedNamePart().Replace(result, m => $"{m.Groups[1].Value}={Redacted}");
        result = EmailOrUpn().Replace(result, Redacted);
        result = DownLevelLogon().Replace(result, Redacted);
        result = QuotedValue().Replace(result, m => $"{m.Value[0]}{Redacted}{m.Value[0]}");
        return result;
    }

    public static SentryEvent Scrub(SentryEvent evt)
    {
        evt.User = new SentryUser();
        ScrubRequest(evt.Request);

        if (evt.Message is { } message)
        {
            evt.Message = new SentryMessage { Formatted = Redact(message.Formatted ?? message.Message) };
        }

        foreach (var exception in evt.SentryExceptions ?? [])
        {
            exception.Value = Redact(exception.Value);
        }

        foreach (var (key, value) in evt.Tags.ToArray())
        {
            evt.SetTag(key, Redact(value) ?? string.Empty);
        }

        foreach (var key in evt.Extra.Keys.ToArray())
        {
            evt.SetExtra(key, Redacted);
        }

        // Module paths reveal the install or build directory (often a user profile); keep only the file name.
        foreach (var image in evt.DebugImages ?? [])
        {
            image.CodeFile = image.CodeFile is { } file ? Path.GetFileName(file.Replace('\\', '/')) : null;
        }

        return evt;
    }

    public static SentryTransaction Scrub(SentryTransaction transaction)
    {
        transaction.User = new SentryUser();
        ScrubRequest(transaction.Request);
        transaction.Description = Redact(transaction.Description);

        foreach (var span in transaction.Spans)
        {
            span.Description = Redact(span.Description);
        }

        foreach (var (key, value) in transaction.Tags.ToArray())
        {
            transaction.SetTag(key, Redact(value) ?? string.Empty);
        }

        return transaction;
    }

    // BeforeBreadcrumb runs when the breadcrumb is recorded, so the rebuilt timestamp matches.
    public static Breadcrumb Scrub(Breadcrumb breadcrumb) =>
        new(
            Redact(breadcrumb.Message) ?? string.Empty,
            breadcrumb.Type ?? string.Empty,
            null,
            breadcrumb.Category,
            breadcrumb.Level);

    private static void ScrubRequest(SentryRequest request)
    {
        request.Cookies = null;
        request.QueryString = null;
        request.Data = null;
        request.Env.Clear();
        request.Other.Clear();

        if (Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
        {
            request.Url = $"{uri.Scheme}://{uri.Authority}{Redact(uri.AbsolutePath)}";
        }
        else
        {
            request.Url = Redact(request.Url);
        }

        foreach (var name in request.Headers.Keys.ToArray())
        {
            if (!SafeHeaders.Contains(name))
            {
                request.Headers.Remove(name);
            }
        }
    }
}
