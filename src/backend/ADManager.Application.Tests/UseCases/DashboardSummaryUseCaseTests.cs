using ADManager.Application.Tests.Fakes;
using ADManager.Application.UseCases;
using ADManager.Domain;
using Xunit;

namespace ADManager.Application.Tests.UseCases;

public sealed class DashboardSummaryUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_CountsLockedOutAndExpiringSoon()
    {
        var gateway = new FakeActiveDirectoryGateway();
        gateway.Users.Add(new AdUser { Username = "locked1", IsLockedOut = true, Enabled = true });
        gateway.Users.Add(new AdUser { Username = "ok1", IsLockedOut = false, Enabled = true, PasswordLastSet = DateTime.UtcNow.AddDays(-10) });
        gateway.Users.Add(new AdUser { Username = "expiring1", IsLockedOut = false, Enabled = true, PasswordLastSet = DateTime.UtcNow.AddDays(-95) });
        var audit = new FakeAuditLogger();
        audit.Entries.Add(new AuditEntry(DateTimeOffset.UtcNow, "admin1", "ResetPassword", "ok1", true, null));
        var useCase = new DashboardSummaryUseCase(gateway, audit, 90);

        var result = await useCase.ExecuteAsync("ou", "admin1");

        Assert.True(result.Success);
        Assert.Equal(1, result.Value!.LockedOutCount);
        Assert.Equal(1, result.Value.ExpiringSoonCount);
        Assert.Single(result.Value.RecentActivity);
    }

    [Fact]
    public async Task ExecuteAsync_CallsSearchUsersWithIncludeGroupsFalse()
    {
        var gateway = new FakeActiveDirectoryGateway();
        var audit = new FakeAuditLogger();
        var useCase = new DashboardSummaryUseCase(gateway, audit, 90);

        await useCase.ExecuteAsync("ou", "admin1");

        Assert.False(gateway.LastIncludeGroups);
    }
}
