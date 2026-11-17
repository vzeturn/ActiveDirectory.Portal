using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class BulkGroupAssignmentUseCaseTests
{
    private static (BulkGroupAssignmentUseCase useCase, FakeActiveDirectoryGateway gateway) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var membership = new ManageGroupMembershipUseCase(gateway, audit);
        return (new BulkGroupAssignmentUseCase(membership), gateway);
    }

    [Fact]
    public async Task ExecuteAsync_AddsAllUsersToAllGroups()
    {
        var (useCase, gateway) = Build();

        var result = await useCase.ExecuteAsync(new BulkGroupAssignmentCommand(new[] { "u1", "u2" }, new[] { "GroupA", "GroupB" }, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.All(result.Value!, r => Assert.True(r.Success));
        Assert.Contains("GroupA", gateway.Memberships["u1"]);
        Assert.Contains("GroupB", gateway.Memberships["u1"]);
        Assert.Contains("GroupA", gateway.Memberships["u2"]);
    }

    [Fact]
    public async Task ExecuteAsync_NoUsernames_Fails()
    {
        var (useCase, _) = Build();

        var result = await useCase.ExecuteAsync(new BulkGroupAssignmentCommand(Array.Empty<string>(), new[] { "GroupA" }, "ou", "admin1"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ExecuteAsync_NoGroupNames_Fails()
    {
        var (useCase, _) = Build();

        var result = await useCase.ExecuteAsync(new BulkGroupAssignmentCommand(new[] { "u1" }, Array.Empty<string>(), "ou", "admin1"));

        Assert.False(result.Success);
    }
}
