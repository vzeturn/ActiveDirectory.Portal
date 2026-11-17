using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class ToggleAccountUseCaseTests
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

    private static (ToggleAccountUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit, FakeNotificationSender notifications) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        return (new ToggleAccountUseCase(gateway, audit, notifications), gateway, audit, notifications);
    }

    [Fact]
    public async Task EnableAsync_ExistingUser_EnablesAndNotifies()
    {
        var (useCase, gateway, audit, notifications) = Build();
        gateway.Users.Add(new AdUser { Username = "jdoe", Enabled = false });

        var result = await useCase.EnableAsync(new AccountCommand("jdoe", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.True(gateway.Users.Single().Enabled);
        Assert.Contains(audit.Entries, e => e.Operation == "EnableAccount" && e.Result);
        Assert.Single(notifications.Sent);
    }

    [Fact]
    public async Task DisableAsync_ExistingUser_DisablesAndNotifies()
    {
        var (useCase, gateway, _, notifications) = Build();
        gateway.Users.Add(new AdUser { Username = "jdoe", Enabled = true });

        var result = await useCase.DisableAsync(new AccountCommand("jdoe", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.False(gateway.Users.Single().Enabled);
        Assert.Single(notifications.Sent);
    }

    [Fact]
    public async Task UnlockAsync_LockedUser_UnlocksAndNotifies()
    {
        var (useCase, gateway, _, notifications) = Build();
        gateway.Users.Add(new AdUser { Username = "jdoe", IsLockedOut = true });

        var result = await useCase.UnlockAsync(new AccountCommand("jdoe", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.False(gateway.Users.Single().IsLockedOut);
        Assert.Single(notifications.Sent);
    }

    [Fact]
    public async Task EnableAsync_EmptyUsername_FailsWithoutCallingGateway()
    {
        var (useCase, _, _, notifications) = Build();

        var result = await useCase.EnableAsync(new AccountCommand("", "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Empty(notifications.Sent);
    }

    private sealed class ThrowingGateway : FakeActiveDirectoryGateway
    {
        public override Task EnableUserAsync(string username, string scope, CancellationToken ct = default) =>
            throw new InvalidOperationException("AD write failed");
    }

    [Fact]
    public async Task EnableAsync_GatewayThrows_ReturnsFailureLogsAndDoesNotNotify()
    {
        var gateway = new ThrowingGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var useCase = new ToggleAccountUseCase(gateway, audit, notifications);

        var result = await useCase.EnableAsync(new AccountCommand("jdoe", "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => !e.Result);
        Assert.Empty(notifications.Sent);
    }
}
