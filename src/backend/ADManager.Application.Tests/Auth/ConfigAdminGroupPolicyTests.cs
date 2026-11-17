using ADManager.Application.Abstractions;
using ADManager.Infrastructure.ActiveDirectory;
using Microsoft.Extensions.Options;
using Xunit;

namespace ADManager.Application.Tests.Auth;

public sealed class ConfigAdminGroupPolicyTests
{
    private static ConfigAdminGroupPolicy Build(string adminGroup, string viewerGroup) =>
        new(Options.Create(new AdSettings { AdminGroupName = adminGroup, ViewerGroupName = viewerGroup }));

    [Fact]
    public void IsAdmin_MemberOfAdminGroup_ReturnsTrue()
    {
        var policy = Build("Corp.IT.Admin", "Corp.IT.Viewer");

        Assert.True(policy.IsAdmin(new[] { "Corp.IT.Admin" }));
    }

    [Fact]
    public void IsAdmin_NotMember_ReturnsFalse()
    {
        var policy = Build("Corp.IT.Admin", "Corp.IT.Viewer");

        Assert.False(policy.IsAdmin(new[] { "SomeOtherGroup" }));
    }

    [Fact]
    public void IsViewer_MemberOfViewerGroup_ReturnsTrue()
    {
        var policy = Build("Corp.IT.Admin", "Corp.IT.Viewer");

        Assert.True(policy.IsViewer(new[] { "Corp.IT.Viewer" }));
    }

    [Fact]
    public void IsViewer_ViewerGroupNameEmpty_AlwaysReturnsFalse()
    {
        var policy = Build("Corp.IT.Admin", "");

        Assert.False(policy.IsViewer(new[] { "AnyGroup" }));
    }
}
