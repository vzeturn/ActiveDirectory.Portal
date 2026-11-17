using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class BulkAccountOpUseCaseTests
{
    private sealed class FakeNotificationSender : INotificationSender
    {
        public List<NotificationMessage> Sent { get; } = new();

        public Task NotifyAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static (BulkAccountOpUseCase useCase, FakeActiveDirectoryGateway gateway) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var toggle = new ToggleAccountUseCase(gateway, audit, new FakeNotificationSender());
        return (new BulkAccountOpUseCase(toggle), gateway);
    }

    [Fact]
    public async Task ExecuteAsync_AllSucceed_ReturnsAllSuccessResults()
    {
        var (useCase, gateway) = Build();
        gateway.Users.Add(new Domain.AdUser { Username = "u1", Enabled = true });
        gateway.Users.Add(new Domain.AdUser { Username = "u2", Enabled = true });

        var result = await useCase.ExecuteAsync(new BulkAccountOpCommand(new[] { "u1", "u2" }, "disable", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.All(result.Value!, r => Assert.True(r.Success));
        Assert.False(gateway.Users.Single(u => u.Username == "u1").Enabled);
        Assert.False(gateway.Users.Single(u => u.Username == "u2").Enabled);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUsernames_Fails()
    {
        var (useCase, _) = Build();

        var result = await useCase.ExecuteAsync(new BulkAccountOpCommand(Array.Empty<string>(), "disable", "ou", "admin1"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownOperation_Fails()
    {
        var (useCase, _) = Build();

        var result = await useCase.ExecuteAsync(new BulkAccountOpCommand(new[] { "u1" }, "delete", "ou", "admin1"));

        Assert.False(result.Success);
    }
}
