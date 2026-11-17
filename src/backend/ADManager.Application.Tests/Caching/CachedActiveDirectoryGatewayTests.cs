using ADManager.Application.Tests.Fakes;
using ADManager.Domain;
using ADManager.Infrastructure.ActiveDirectory;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace ADManager.Application.Tests.Caching;

public sealed class CachedActiveDirectoryGatewayTests
{
    private static (CachedActiveDirectoryGateway sut, FakeActiveDirectoryGateway inner) CreateSut()
    {
        var inner = new FakeActiveDirectoryGateway();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var generations = new AdCacheGenerationStore();
        var sut = new CachedActiveDirectoryGateway(inner, cache, generations);
        return (sut, inner);
    }

    [Fact]
    public async Task GetUserAsync_SecondCallWithinTtl_DoesNotHitInner()
    {
        var (sut, inner) = CreateSut();
        inner.Users.Add(new AdUser { Username = "alice" });

        await sut.GetUserAsync("alice", "ou");
        await sut.GetUserAsync("alice", "ou");

        Assert.Equal(1, inner.CallCounts[nameof(inner.GetUserAsync)]);
    }

    [Fact]
    public async Task GetUserAsync_AfterEnableUserAsync_HitsInnerAgain()
    {
        var (sut, inner) = CreateSut();
        inner.Users.Add(new AdUser { Username = "alice", Enabled = false });

        await sut.GetUserAsync("alice", "ou");
        await sut.EnableUserAsync("alice", "ou");
        await sut.GetUserAsync("alice", "ou");

        Assert.Equal(2, inner.CallCounts[nameof(inner.GetUserAsync)]);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_AlwaysHitsInner()
    {
        var (sut, inner) = CreateSut();
        inner.Credentials["alice"] = "pw";

        await sut.ValidateCredentialsAsync("alice", "pw");
        await sut.ValidateCredentialsAsync("alice", "pw");

        Assert.Equal(2, inner.CallCounts[nameof(inner.ValidateCredentialsAsync)]);
    }

    [Fact]
    public async Task CanBindAsync_AlwaysHitsInner()
    {
        var (sut, inner) = CreateSut();

        await sut.CanBindAsync();
        await sut.CanBindAsync();

        Assert.Equal(2, inner.CallCounts[nameof(inner.CanBindAsync)]);
    }

    [Fact]
    public async Task SearchUsersAsync_AfterUserWrite_BumpsGenerationAndMissesCache()
    {
        var (sut, inner) = CreateSut();
        inner.Users.Add(new AdUser { Username = "alice" });

        await sut.SearchUsersAsync("ali", 10, "ou");
        await sut.ResetPasswordAsync("alice", "newpw", true, "ou");
        await sut.SearchUsersAsync("ali", 10, "ou");

        Assert.Equal(2, inner.CallCounts[nameof(inner.SearchUsersAsync)]);
    }

    [Fact]
    public async Task EnableUserAsync_EvictingNeverCachedUser_DoesNotThrow()
    {
        var (sut, _) = CreateSut();

        var exception = await Record.ExceptionAsync(() => sut.EnableUserAsync("never-read", "ou"));

        Assert.Null(exception);
    }

    [Fact]
    public async Task GetGroupAsync_AfterAddUserToGroups_HitsInnerAgain()
    {
        var (sut, inner) = CreateSut();
        inner.Groups.Add(new AdGroup { Name = "grp" });
        inner.Users.Add(new AdUser { Username = "alice" });

        await sut.GetGroupAsync("grp", "ou");
        await sut.AddUserToGroupsAsync("alice", ["grp"], "ou");
        await sut.GetGroupAsync("grp", "ou");

        Assert.Equal(2, inner.CallCounts[nameof(inner.GetGroupAsync)]);
    }
}
