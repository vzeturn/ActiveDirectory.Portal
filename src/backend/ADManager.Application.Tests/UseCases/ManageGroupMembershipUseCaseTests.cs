using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class ManageGroupMembershipUseCaseTests
{
    private static (ManageGroupMembershipUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        return (new ManageGroupMembershipUseCase(gateway, audit), gateway, audit);
    }

    [Fact]
    public async Task GetMembershipAsync_ReturnsSeededGroups()
    {
        var (useCase, gateway, _) = Build();
        gateway.Memberships["jdoe"] = new List<string> { "Corp.Sales" };

        var result = await useCase.GetMembershipAsync(new GroupMembershipQuery("jdoe", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(new[] { "Corp.Sales" }, result.Value);
    }

    [Fact]
    public async Task AddAsync_ValidGroups_AddsAndLogs()
    {
        var (useCase, gateway, audit) = Build();

        var result = await useCase.AddAsync(new ChangeGroupMembershipCommand("jdoe", new[] { "Corp.IT.Admin", "Corp.Sales" }, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(new[] { "Corp.IT.Admin", "Corp.Sales" }, gateway.Memberships["jdoe"]);
        Assert.Contains(audit.Entries, e => e.Operation == "AddToGroups" && e.Result);
    }

    [Fact]
    public async Task AddAsync_EmptyGroupList_Fails()
    {
        var (useCase, _, _) = Build();

        var result = await useCase.AddAsync(new ChangeGroupMembershipCommand("jdoe", Array.Empty<string>(), "ou", "admin1"));

        Assert.False(result.Success);
    }

    [Fact]
    public async Task RemoveAsync_SingleGroupNameArray_RemovesIt()
    {
        var (useCase, gateway, _) = Build();
        gateway.Memberships["jdoe"] = new List<string> { "Corp.IT.Admin", "Corp.Sales" };

        var result = await useCase.RemoveAsync(new ChangeGroupMembershipCommand("jdoe", new[] { "Corp.Sales" }, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(new[] { "Corp.IT.Admin" }, gateway.Memberships["jdoe"]);
    }

    [Fact]
    public async Task AddAsync_MutationCheck_SilentAddIsCaughtByAssertionOnMemberships()
    {
        // Arms a gateway that reports success but never actually mutates membership —
        // this MUST fail the assertion below, proving the happy-path test is not vacuous.
        var gateway = new ArmedOnMutationGateway.SilentAdd();
        var audit = new FakeAuditLogger();
        var useCase = new ManageGroupMembershipUseCase(gateway, audit);

        var result = await useCase.AddAsync(new ChangeGroupMembershipCommand("jdoe", new[] { "Corp.IT.Admin" }, "ou", "admin1"));

        // The use case still reports success (it trusts the gateway) — the regression
        // shows up as the membership never actually changing, which the real
        // AddAsync_ValidGroups_AddsAndLogs test's Memberships assertion would catch.
        Assert.True(result.Success);
        Assert.False(gateway.Memberships.ContainsKey("jdoe"));
    }

    [Fact]
    public async Task RemoveAsync_MutationCheck_GatewayThrow_IsCaughtAndLogged()
    {
        var gateway = new ArmedOnMutationGateway.ThrowingRemove();
        var audit = new FakeAuditLogger();
        var useCase = new ManageGroupMembershipUseCase(gateway, audit);

        var result = await useCase.RemoveAsync(new ChangeGroupMembershipCommand("jdoe", new[] { "Corp.Sales" }, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => e.Operation == "RemoveFromGroup" && !e.Result);
    }
}
