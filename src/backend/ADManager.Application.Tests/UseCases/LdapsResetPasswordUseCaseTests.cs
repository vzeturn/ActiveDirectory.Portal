using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class LdapsResetPasswordUseCaseTests
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

    [Fact]
    public async Task ExecuteAsync_Success_SendsDiscordNotificationWithPassword()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var useCase = new LdapsResetPasswordUseCase(gateway, audit, notifications);

        var result = await useCase.ExecuteAsync(new LdapsResetPasswordCommand("jdoe", "Manual1!Pass", "ou", "admin1"));

        Assert.True(result.Success);
        var sent = Assert.Single(notifications.Sent);
        Assert.Equal("Manual1!Pass", sent.Fields["NewPassword"]);
        Assert.Equal("jdoe", sent.Fields["User"]);
    }

    [Fact]
    public async Task ExecuteAsync_GatewayThrows_DoesNotSendNotification()
    {
        var gateway = new ThrowingGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var useCase = new LdapsResetPasswordUseCase(gateway, audit, notifications);

        var result = await useCase.ExecuteAsync(new LdapsResetPasswordCommand("jdoe", "Manual1!Pass", "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Empty(notifications.Sent);
    }

    private sealed class ThrowingGateway : FakeActiveDirectoryGateway
    {
        public override Task ResetPasswordViaLdapsAsync(string username, string newPassword, string scope, CancellationToken ct = default) =>
            throw new InvalidOperationException("LDAPS reset failed");
    }
}
