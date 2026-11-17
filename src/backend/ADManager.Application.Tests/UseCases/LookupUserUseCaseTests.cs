using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class LookupUserUseCaseTests
{
    private static (LookupUserUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        return (new LookupUserUseCase(gateway, audit), gateway, audit);
    }

    [Fact]
    public async Task SearchAsync_MatchingUsers_ReturnsThemAndLogsCount()
    {
        var (useCase, gateway, audit) = Build();
        gateway.Users.Add(new AdUser { Username = "jdoe", DisplayName = "John Doe" });
        gateway.Users.Add(new AdUser { Username = "jsmith", DisplayName = "Jane Smith" });

        var result = await useCase.SearchAsync(new SearchUsersQuery("j", 100, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(audit.Entries, e => e.Operation == "SearchUsers" && e.Details == "2 results");
    }

    [Fact]
    public async Task GetAsync_ExistingUser_ReturnsUser()
    {
        var (useCase, gateway, _) = Build();
        gateway.Users.Add(new AdUser { Username = "jdoe", DisplayName = "John Doe" });

        var result = await useCase.GetAsync(new GetUserQuery("jdoe", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal("John Doe", result.Value!.DisplayName);
    }

    [Fact]
    public async Task GetAsync_MissingUser_ReturnsSuccessWithNullValue()
    {
        var (useCase, _, _) = Build();

        var result = await useCase.GetAsync(new GetUserQuery("ghost", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }

    private sealed class ThrowingGateway : FakeActiveDirectoryGateway
    {
        public override Task<IReadOnlyList<AdUser>> SearchUsersAsync(string query, int limit, string scope, bool includeGroups = true, CancellationToken ct = default) =>
            throw new InvalidOperationException("AD unreachable");
    }

    [Fact]
    public async Task SearchAsync_GatewayThrows_ReturnsFailureAndLogsError()
    {
        var gateway = new ThrowingGateway();
        var audit = new FakeAuditLogger();
        var useCase = new LookupUserUseCase(gateway, audit);

        var result = await useCase.SearchAsync(new SearchUsersQuery("j", 100, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => e.Operation == "SearchUsers" && !e.Result);
    }
}
