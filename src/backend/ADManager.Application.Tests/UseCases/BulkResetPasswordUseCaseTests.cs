using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class BulkResetPasswordUseCaseTests
{
    private sealed class FixedPasswordGenerator : IPasswordGenerator
    {
        public string Generate() => "@Qua241224!VN7";
    }

    private sealed class FakeNotificationSender : INotificationSender
    {
        public List<NotificationMessage> Sent { get; } = new();
        public Task NotifyAsync(NotificationMessage message, CancellationToken ct = default)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private static (BulkResetPasswordUseCase useCase, FakeActiveDirectoryGateway gateway, FakeNotificationSender notifications) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var reset = new ResetPasswordUseCase(gateway, new FixedPasswordGenerator(), audit, notifications);
        return (new BulkResetPasswordUseCase(reset), gateway, notifications);
    }

    [Fact]
    public async Task ExecuteAsync_ResetsAllUsers_EachGetsOwnNotification()
    {
        var (useCase, gateway, notifications) = Build();

        var result = await useCase.ExecuteAsync(new BulkResetPasswordCommand(new[] { "u1", "u2" }, true, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.All(result.Value!, r => Assert.True(r.Success));
        Assert.Equal("@Qua241224!VN7", gateway.Credentials["u1"]);
        Assert.Equal("@Qua241224!VN7", gateway.Credentials["u2"]);
        Assert.Equal(2, notifications.Sent.Count);
    }

    [Fact]
    public async Task ExecuteAsync_NoUsernames_Fails()
    {
        var (useCase, _, _) = Build();

        var result = await useCase.ExecuteAsync(new BulkResetPasswordCommand(Array.Empty<string>(), true, "ou", "admin1"));

        Assert.False(result.Success);
    }
}
