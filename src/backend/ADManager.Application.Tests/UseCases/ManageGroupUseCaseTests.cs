using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class ManageGroupUseCaseTests
{
    private static (ManageGroupUseCase useCase, FakeActiveDirectoryGateway gateway, FakeAuditLogger audit) Build()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        return (new ManageGroupUseCase(gateway, audit), gateway, audit);
    }

    [Fact]
    public async Task CreateAsync_NewGroup_Succeeds()
    {
        var (useCase, gateway, audit) = Build();

        var result = await useCase.CreateAsync(new CreateGroupCommand("NewTeam", "A new team", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Contains(gateway.Groups, g => g.Name == "NewTeam" && g.Description == "A new team");
        Assert.Contains(audit.Entries, e => e.Operation == "CreateGroup" && e.Result);
    }

    [Fact]
    public async Task CreateAsync_GroupAlreadyExists_Fails()
    {
        var (useCase, gateway, audit) = Build();
        gateway.Groups.Add(new AdGroup { Name = "Existing" });

        var result = await useCase.CreateAsync(new CreateGroupCommand("Existing", null, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => e.Operation == "CreateGroup" && !e.Result);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_FailsWithoutCallingGateway()
    {
        var (useCase, gateway, _) = Build();

        var result = await useCase.CreateAsync(new CreateGroupCommand("", null, "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Empty(gateway.Groups);
    }

    [Fact]
    public async Task DeleteAsync_ExistingGroup_Succeeds()
    {
        var (useCase, gateway, audit) = Build();
        gateway.Groups.Add(new AdGroup { Name = "ToDelete" });

        var result = await useCase.DeleteAsync(new DeleteGroupCommand("ToDelete", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.DoesNotContain(gateway.Groups, g => g.Name == "ToDelete");
        Assert.Contains(audit.Entries, e => e.Operation == "DeleteGroup" && e.Result);
    }

    [Fact]
    public async Task DeleteAsync_MissingGroup_Fails()
    {
        var (useCase, _, audit) = Build();

        var result = await useCase.DeleteAsync(new DeleteGroupCommand("Missing", "ou", "admin1"));

        Assert.False(result.Success);
        Assert.Contains(audit.Entries, e => e.Operation == "DeleteGroup" && !e.Result);
    }

    [Fact]
    public async Task UpdateDescriptionAsync_ExistingGroup_Succeeds()
    {
        var (useCase, gateway, audit) = Build();
        gateway.Groups.Add(new AdGroup { Name = "Team", Description = "Old" });

        var result = await useCase.UpdateDescriptionAsync(new UpdateGroupDescriptionCommand("Team", "New", "ou", "admin1"));

        Assert.True(result.Success);
        Assert.Equal("New", gateway.Groups.Single(g => g.Name == "Team").Description);
        Assert.Contains(audit.Entries, e => e.Operation == "UpdateGroupDescription" && e.Result);
    }
}
