using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class LoginUseCaseTests
{
    private static (LoginUseCase useCase, FakeActiveDirectoryGateway gateway, FakeSessionService sessions, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var sessions = new FakeSessionService();
        var audit = new FakeAuditLogger();
        return (new LoginUseCase(gateway, sessions, audit), gateway, sessions, audit);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_ReturnsSessionAndLogsSuccess()
    {
        var (useCase, gateway, _, audit) = Build();
        gateway.Credentials["jdoe"] = "correct-password";
        gateway.Users.Add(new AdUser { Username = "jdoe", DisplayName = "John Doe", Enabled = true, Groups = new[] { "Corp.IT.Admin" } });

        var result = await useCase.ExecuteAsync(new LoginCommand("jdoe", "correct-password"));

        Assert.True(result.Success);
        Assert.Equal("jdoe", result.Value!.Username);
        Assert.Equal("John Doe", result.Value.DisplayName);
        Assert.Contains(audit.Entries, e => e.Operation == "Login" && e.Result);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidCredentials_FailsAndLogsFailure()
    {
        var (useCase, gateway, _, audit) = Build();
        gateway.Credentials["jdoe"] = "correct-password";

        var result = await useCase.ExecuteAsync(new LoginCommand("jdoe", "wrong-password"));

        Assert.False(result.Success);
        Assert.Null(result.Value);
        Assert.Contains(audit.Entries, e => e.Operation == "Login" && !e.Result);
    }

    [Theory]
    [InlineData("", "password")]
    [InlineData("jdoe", "")]
    [InlineData(" ", " ")]
    public async Task ExecuteAsync_MissingCredentials_FailsWithoutCallingGateway(string username, string password)
    {
        var (useCase, _, sessions, _) = Build();

        var result = await useCase.ExecuteAsync(new LoginCommand(username, password));

        Assert.False(result.Success);
        Assert.Empty(sessions.Sessions);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_UsernameFallsBackToDisplayNameWhenUserNotFound()
    {
        var (useCase, gateway, _, _) = Build();
        gateway.Credentials["ghost"] = "pw";
        // No AdUser seeded for "ghost" — GetUserAsync returns null.

        var result = await useCase.ExecuteAsync(new LoginCommand("ghost", "pw"));

        Assert.True(result.Success);
        Assert.Equal("ghost", result.Value!.DisplayName);
        Assert.Empty(result.Value.Groups);
    }
}
