namespace ADManager.Application.Tests.Fakes;

/// <summary>
/// Subclasses FakeActiveDirectoryGateway to inject a single targeted failure per test,
/// proving the corresponding use-case test would actually go red if the real behavior
/// regressed. Each nested type below "arms" exactly one seam.
/// </summary>
public static class ArmedOnMutationGateway
{
    /// <summary>Arms AddUserToGroupsAsync to silently no-op instead of adding — proves
    /// ManageGroupMembershipUseaseTests.AddAsync_ValidGroups_AddsAndLogs would fail if the
    /// real AddUserToGroupsAsync implementation stopped mutating the underlying AD group.</summary>
    public sealed class SilentAdd : FakeActiveDirectoryGateway
    {
        public override Task AddUserToGroupsAsync(string username, IReadOnlyList<string> groupNames, string scope, CancellationToken ct = default) =>
            Task.CompletedTask; // Deliberately does NOT mutate Memberships.
    }

    /// <summary>Arms RemoveUserFromGroupAsync to throw — proves error-path assertions
    /// (audit entry with Result=false, OperationResult.Success=false) are load-bearing.</summary>
    public sealed class ThrowingRemove : FakeActiveDirectoryGateway
    {
        public override Task RemoveUserFromGroupAsync(string username, string groupName, string scope, CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated AD write failure.");
    }
}
