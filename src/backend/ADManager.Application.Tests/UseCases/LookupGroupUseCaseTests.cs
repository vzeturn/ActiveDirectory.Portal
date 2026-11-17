using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class LookupGroupUseCaseTests
{
    private static (LookupGroupUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        return (new LookupGroupUseCase(gateway, audit), gateway, audit);
    }

    [Fact]
    public async Task SearchAsync_MatchingGroups_ReturnsThem()
    {
        var (useCase, gateway, _) = Build();
        gateway.Groups.Add(new AdGroup { Name = "Corp.IT.Admin" });
        gateway.Groups.Add(new AdGroup { Name = "Corp.Sales" });

        var result = await useCase.SearchAsync(new SearchGroupsQuery("Corp", 100, "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(2, result.Value!.Count);
    }

    [Fact]
    public async Task GetAsync_ExistingGroup_ReturnsGroup()
    {
        var (useCase, gateway, _) = Build();
        gateway.Groups.Add(new AdGroup { Name = "Corp.IT.Admin", MemberCount = 5 });

        var result = await useCase.GetAsync(new GetGroupQuery("Corp.IT.Admin", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal(5, result.Value!.MemberCount);
    }

    [Fact]
    public async Task GetAsync_MissingGroup_ReturnsSuccessWithNullValue()
    {
        var (useCase, _, _) = Build();

        var result = await useCase.GetAsync(new GetGroupQuery("ghost-group", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Null(result.Value);
    }
}
