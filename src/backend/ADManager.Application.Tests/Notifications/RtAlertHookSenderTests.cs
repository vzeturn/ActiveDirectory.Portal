using System.Net;
using System.Text.Json;
using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Notifications;

public sealed class RtAlertHookSenderTests
{
    private const string HookUrl = "https://rt.example.test/hooks/alerts/ad-manager/token";

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.Accepted;
        public bool Throw { get; set; }
        public List<(Uri Url, string Body)> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (Throw) throw new HttpRequestException("network down");
            Requests.Add((request.RequestUri!, await request.Content!.ReadAsStringAsync(ct)));
            return new HttpResponseMessage(Status);
        }
    }

    private static (RtAlertHookSender Sender, RecordingHandler Handler) Build(string? hookUrl = HookUrl, string? tenant = "haier-ad")
    {
        var handler = new RecordingHandler();
        var sender = new RtAlertHookSender(
            new HttpClient(handler),
            new FakeSecretsProvider { AlertHookUrl = hookUrl },
            Options.Create(new AlertSettings { Tenant = tenant }),
            NullLogger<RtAlertHookSender>.Instance);
        return (sender, handler);
    }

    private static NotificationMessage Message(AlertSeverity severity, Dictionary<string, string>? fields = null) =>
        new("Password Reset (partial): jdoe", "Follow-up step failed", severity,
            fields ?? new Dictionary<string, string> { ["User"] = "jdoe" },
            new DateTimeOffset(2026, 11, 21, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task SendAsync_Info_IsOnlyLogged()
    {
        var (sender, handler) = Build();

        var result = await sender.SendAsync(Message(AlertSeverity.Info));

        Assert.Equal(AlertDeliveryResult.LoggedOnly, result);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(AlertSeverity.Warning, "warning")]
    [InlineData(AlertSeverity.Critical, "critical")]
    public async Task SendAsync_WarningOrCritical_PostsHubPayload(AlertSeverity severity, string expected)
    {
        var (sender, handler) = Build();

        var result = await sender.SendAsync(Message(severity));

        Assert.True(result.IsDelivered);
        var (url, body) = Assert.Single(handler.Requests);
        Assert.Equal(HookUrl, url.ToString());
        using var json = JsonDocument.Parse(body);
        var root = json.RootElement;
        Assert.Equal(expected, root.GetProperty("severity").GetString());
        Assert.Equal("Password Reset (partial): jdoe", root.GetProperty("title").GetString());
        Assert.Equal("haier-ad", root.GetProperty("tenant").GetString());
        Assert.Contains("User: jdoe", root.GetProperty("text").GetString());
        Assert.Equal(4, root.EnumerateObject().Count());
    }

    [Fact]
    public async Task SendAsync_DropsPasswordLikeFields()
    {
        var (sender, handler) = Build();
        var fields = new Dictionary<string, string> { ["User"] = "jdoe", ["NewPassword"] = "S3cret!Value" };

        await sender.SendAsync(Message(AlertSeverity.Warning, fields));

        var (_, body) = Assert.Single(handler.Requests);
        Assert.DoesNotContain("S3cret!Value", body);
    }

    [Fact]
    public async Task SendAsync_NoHookConfigured_ReportsNotConfigured()
    {
        var (sender, handler) = Build(hookUrl: null);

        var result = await sender.SendAsync(Message(AlertSeverity.Warning));

        Assert.Equal(AlertDeliveryResult.NotConfigured, result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SendAsync_HubRejects_ReportsStatus()
    {
        var (sender, handler) = Build();
        handler.Status = HttpStatusCode.Unauthorized;

        var result = await sender.SendAsync(Message(AlertSeverity.Critical));

        Assert.False(result.IsDelivered);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public async Task NotifyAsync_NetworkFailure_DoesNotThrow()
    {
        var (sender, handler) = Build();
        handler.Throw = true;

        await sender.NotifyAsync(Message(AlertSeverity.Warning));
    }

    [Fact]
    public async Task SendAsync_BlankTenant_FallsBackToMachineName()
    {
        var (sender, handler) = Build(tenant: " ");

        await sender.SendAsync(Message(AlertSeverity.Warning));

        using var json = JsonDocument.Parse(Assert.Single(handler.Requests).Body);
        Assert.Equal(Environment.MachineName, json.RootElement.GetProperty("tenant").GetString());
    }
}
