using ADManager.Application.Abstractions;
using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class ResetPasswordUseCaseTests
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

    private static (ResetPasswordUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var useCase = new ResetPasswordUseCase(gateway, new FixedPasswordGenerator(), audit, new FakeNotificationSender());
        return (useCase, gateway, audit);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitPassword_UsesItAsIs()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", "MyChosenPw1!", false, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal("MyChosenPw1!", result.Value);
        Assert.Equal("MyChosenPw1!", gateway.Credentials["jdoe"]);
    }

    [Fact]
    public async Task ExecuteAsync_NoPasswordSupplied_GeneratesOne()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", null, true, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal("@Qua241224!VN7", result.Value);
        Assert.Equal("@Qua241224!VN7", gateway.Credentials["jdoe"]);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUsername_Fails()
    {
        var (useCase, _, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("", null, false, "ou", "admin1"));

        Assert.False(result.Success);
    }

    private sealed class ThrowingGateway : FakeActiveDirectoryGateway
    {
        public override Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default) =>
            throw new InvalidOperationException("AD reset failed");
    }

    [Fact]
    public async Task ExecuteAsync_GatewayThrows_ReturnsFailureAndLogs()
    {
        var gateway = new ThrowingGateway();
        var audit = new FakeAuditLogger();
        var useCase = new ResetPasswordUseCase(gateway, new FixedPasswordGenerator(), audit, new FakeNotificationSender());

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", null, false, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => e.Operation == "ResetPassword" && !e.Result);
    }

    [Fact]
    public async Task ExecuteAsync_ManualPasswordTooShort_FailsWithoutCallingGateway()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", "Aa1!", false, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.False(gateway.Credentials.ContainsKey("jdoe"));
    }

    [Fact]
    public async Task ExecuteAsync_ManualPasswordMissingCharacterClasses_Fails()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", "alllowercase1", false, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.False(gateway.Credentials.ContainsKey("jdoe"));
    }

    [Fact]
    public async Task ExecuteAsync_ManualPasswordMeetsComplexity_Succeeds()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", "GoodPass1!", false, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal("GoodPass1!", gateway.Credentials["jdoe"]);
    }

    [Fact]
    public async Task ExecuteAsync_Success_NotificationIncludesNewPassword()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var useCase = new ResetPasswordUseCase(gateway, new FixedPasswordGenerator(), audit, notifications);

        await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", null, true, "ou", "admin1"));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal("@Qua241224!VN7", sent.Fields["NewPassword"]);
    }

    private sealed class FollowUpFailsGateway : FakeActiveDirectoryGateway
    {
        public override Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default) =>
            throw new PasswordSetButFollowUpFailedException(newPassword, new InvalidOperationException("Access is denied."));
    }

    [Fact]
    public async Task ExecuteAsync_PartialFailure_NotificationStillIncludesNewPassword()
    {
        var gateway = new FollowUpFailsGateway();
        var audit = new FakeAuditLogger();
        var notifications = new FakeNotificationSender();
        var useCase = new ResetPasswordUseCase(gateway, new FixedPasswordGenerator(), audit, notifications);

        await useCase.ExecuteAsync(new ResetPasswordCommand("jdoe", null, true, "ou", "admin1"));

        var sent = Assert.Single(notifications.Sent);
        Assert.Equal("@Qua241224!VN7", sent.Fields["NewPassword"]);
    }
}
