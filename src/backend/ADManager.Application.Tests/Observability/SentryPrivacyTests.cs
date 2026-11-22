using ADManager.Api.Observability;
using Microsoft.Extensions.Configuration;
using Sentry;
using Sentry.Protocol;
using Xunit;

namespace ADManager.Application.Tests.Observability;

public sealed class SentryPrivacyTests
{
    [Theory]
    [InlineData("Bind failed for CN=John Doe,OU=Staff,DC=corp,DC=local",
        "Bind failed for CN=[redacted],OU=[redacted],DC=[redacted],DC=[redacted]")]
    [InlineData("User jdoe@corp.local is locked", "User [redacted] is locked")]
    [InlineData(@"Logon CORP\jdoe denied", "Logon [redacted] denied")]
    [InlineData("User 'jdoe' not found", "User '[redacted]' not found")]
    [InlineData("PUT /api/users/jdoe/disable failed", "PUT /api/users/[redacted]/disable failed")]
    [InlineData("DELETE /api/groups/Sales/members/jdoe", "DELETE /api/groups/[redacted]/members/[redacted]")]
    [InlineData("Timeout talking to the directory", "Timeout talking to the directory")]
    public void Redact_RemovesDirectoryIdentifiers(string input, string expected)
    {
        Assert.Equal(expected, SentryPrivacy.Redact(input));
    }

    [Fact]
    public void Scrub_Event_DropsUserRequestDetailsAndRedactsMessages()
    {
        var evt = new SentryEvent(new InvalidOperationException("User 'jdoe' not found"))
        {
            User = new SentryUser { Username = "jdoe", IpAddress = "10.0.0.5", Email = "jdoe@corp.local" },
            Message = new SentryMessage { Formatted = "Unhandled exception for PUT /api/users/jdoe/enable." },
        };
        evt.SentryExceptions = [new SentryException { Type = "InvalidOperationException", Value = "User 'jdoe' not found" }];
        evt.Request.Url = "https://ad.example.com/api/users/jdoe/groups?q=jdoe";
        evt.Request.QueryString = "q=jdoe";
        evt.Request.Cookies = "ADManager.Session=abc";
        evt.Request.Headers["Cookie"] = "ADManager.Session=abc";
        evt.Request.Headers["X-Api-Key"] = "secret";
        evt.Request.Headers["Content-Type"] = "application/json";
        evt.Request.Data = "{\"newPassword\":\"P@ss\"}";
        evt.SetTag("RequestPath", "/api/users/jdoe");
        evt.SetExtra("Path", "/api/users/jdoe");
        evt.DebugImages = [new DebugImage { CodeFile = @"C:\Users\jdoe\app\ADManager.Api.dll" }];

        var scrubbed = SentryPrivacy.Scrub(evt);

        Assert.Null(scrubbed.User.Username);
        Assert.Null(scrubbed.User.IpAddress);
        Assert.Null(scrubbed.User.Email);
        Assert.Equal("https://ad.example.com/api/users/[redacted]/groups", scrubbed.Request.Url);
        Assert.Null(scrubbed.Request.QueryString);
        Assert.Null(scrubbed.Request.Cookies);
        Assert.Null(scrubbed.Request.Data);
        Assert.Equal(["Content-Type"], scrubbed.Request.Headers.Keys);
        Assert.Equal("Unhandled exception for PUT /api/users/[redacted]/enable.", scrubbed.Message?.Formatted);
        Assert.Equal("User '[redacted]' not found", Assert.Single(scrubbed.SentryExceptions!).Value);
        Assert.Equal("/api/users/[redacted]", scrubbed.Tags["RequestPath"]);
        Assert.Equal(SentryPrivacy.Redacted, scrubbed.Extra["Path"]);
        Assert.Equal("ADManager.Api.dll", Assert.Single(scrubbed.DebugImages!).CodeFile);
    }

    [Fact]
    public void Scrub_Breadcrumb_RedactsMessageAndDropsData()
    {
        var breadcrumb = new Breadcrumb(
            "Reset password for CN=Jane,DC=corp",
            "default",
            new Dictionary<string, string> { ["user"] = "jane" },
            "ADManager",
            BreadcrumbLevel.Warning);

        var scrubbed = SentryPrivacy.Scrub(breadcrumb);

        Assert.Equal("Reset password for CN=[redacted],DC=[redacted]", scrubbed.Message);
        Assert.True(scrubbed.Data is null || scrubbed.Data.Count == 0);
        Assert.Equal("ADManager", scrubbed.Category);
    }

    [Fact]
    public void ReadSettings_IsDisabledWithoutDsn()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();

        var settings = SentrySetup.ReadSettings(configuration);

        Assert.Equal(!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SENTRY_DSN")), settings.IsEnabled);
        Assert.Equal("production", settings.Environment);
    }

    [Fact]
    public void Configure_TurnsOffDefaultPii()
    {
        var options = new Sentry.AspNetCore.SentryAspNetCoreOptions();

        SentrySetup.Configure(options, new SentrySettings { Dsn = "https://key@o1.ingest.sentry.io/1", TracesSampleRate = 5 });

        Assert.False(options.SendDefaultPii);
        Assert.True(options.DisableSentryHttpMessageHandler);
        Assert.Equal(1, options.TracesSampleRate);
        Assert.Equal("production", options.Environment);
    }
}
