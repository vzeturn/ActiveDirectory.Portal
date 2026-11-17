using System.DirectoryServices;
using System.Linq;
using ADManager.Application.Abstractions;
using ADManager.Domain;
using Microsoft.Extensions.Logging;

namespace ADManager.Infrastructure.ActiveDirectory;

public sealed class ActiveDirectoryGateway : IActiveDirectoryGateway
{
    // ADS_UF_ACCOUNTDISABLE - bit 2 of userAccountControl.
    private const int AccountDisabledFlag = 0x2;

    private static readonly string[] UserAttributes =
    [
        "sAMAccountName", "displayName", "givenName", "sn", "mail", "userAccountControl",
        "lockoutTime", "distinguishedName", "description", "memberOf"
    ];

    private static readonly string[] GroupAttributes =
    [
        "sAMAccountName", "description", "distinguishedName", "member"
    ];

    private readonly ISecretsProvider _secrets;
    private readonly ILogger<ActiveDirectoryGateway> _logger;

    public ActiveDirectoryGateway(ISecretsProvider secrets, ILogger<ActiveDirectoryGateway> logger)
    {
        _secrets = secrets;
        _logger = logger;
    }

    private T TimedFind<T>(string operation, Func<T> find)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = find();
        stopwatch.Stop();
        _logger.LogDebug("AD LDAP operation {Operation} took {ElapsedMs}ms", operation, stopwatch.ElapsedMilliseconds);
        return result;
    }

    internal static List<List<string>> ChunkIdentifiers(IReadOnlyList<string> identifiers, int batchSize)
    {
        var chunks = new List<List<string>>();
        for (var i = 0; i < identifiers.Count; i += batchSize)
        {
            chunks.Add(identifiers.Skip(i).Take(batchSize).ToList());
        }
        return chunks;
    }

    internal Dictionary<string, SearchResult> BatchFindByDistinguishedName(
        DirectoryEntry root, IReadOnlyList<string> distinguishedNames, string[] attributesToLoad)
    {
        var results = new Dictionary<string, SearchResult>(StringComparer.OrdinalIgnoreCase);
        if (distinguishedNames.Count == 0) return results;

        foreach (var chunk in ChunkIdentifiers(distinguishedNames, 900))
        {
            var filter = "(|" + string.Join(string.Empty, chunk.Select(dn => $"(distinguishedName={EscapeLdapFilterValue(dn)})")) + ")";
            using var searcher = new DirectorySearcher(root) { Filter = filter, PageSize = chunk.Count };
            searcher.PropertiesToLoad.AddRange(attributesToLoad);
            // distinguishedName is the dictionary key below - it must always be loaded even if
            // the caller didn't ask for it, otherwise every result maps to an empty key and the
            // batch lookup silently returns nothing for all identifiers (regression: this used
            // to return 0 resolved names whenever "distinguishedName" wasn't in attributesToLoad).
            if (!searcher.PropertiesToLoad.Contains("distinguishedName"))
            {
                searcher.PropertiesToLoad.Add("distinguishedName");
            }
            using var found = TimedFind($"BatchFindByDistinguishedName({chunk.Count} DNs)", () => searcher.FindAll());
            foreach (SearchResult result in found)
            {
                var dn = GetSingleProperty(result, "distinguishedName");
                if (!string.IsNullOrEmpty(dn))
                {
                    results[dn] = result;
                }
            }
        }
        return results;
    }

    internal static string EscapeSidForFilter(byte[] sid) =>
        "\\" + string.Join("\\", sid.Select(b => b.ToString("x2")));

    private Dictionary<string, SearchResult> BatchFindByObjectSid(
        DirectoryEntry root, IReadOnlyList<byte[]> sids, string[] attributesToLoad)
    {
        var results = new Dictionary<string, SearchResult>();
        if (sids.Count == 0) return results;

        var sidStrings = sids.Select(s => BitConverter.ToString(s)).ToList();
        var bySidString = sids.ToDictionary(s => BitConverter.ToString(s), s => s);

        foreach (var chunk in ChunkIdentifiers(sidStrings, 900))
        {
            var filter = "(|" + string.Join(string.Empty, chunk.Select(s => $"(objectSid={EscapeSidForFilter(bySidString[s])})")) + ")";
            using var searcher = new DirectorySearcher(root) { Filter = filter, PageSize = chunk.Count };
            searcher.PropertiesToLoad.AddRange(attributesToLoad);
            using var found = TimedFind($"BatchFindByObjectSid({chunk.Count} SIDs)", () => searcher.FindAll());
            foreach (SearchResult result in found)
            {
                var name = GetSingleProperty(result, "sAMAccountName");
                if (!string.IsNullOrEmpty(name))
                {
                    results[name] = result;
                }
            }
        }
        return results;
    }

    // sAMAccountName, not CN, is the identifier the rest of the app compares against
    // (admin-group policy, group membership commands) - CN is a display label that can
    // diverge from sAMAccountName (e.g. this forest's numeric-employee-ID user accounts).
    private List<string> ResolveSamAccountNames(DirectoryEntry root, SearchResult result, string propertyName)
    {
        var dns = new List<string>();
        foreach (var value in result.Properties[propertyName])
        {
            var dn = value?.ToString();
            if (!string.IsNullOrEmpty(dn)) dns.Add(dn);
        }
        if (dns.Count == 0) return new List<string>();

        var resolved = BatchFindByDistinguishedName(root, dns, ["sAMAccountName"]);
        var names = new List<string>();
        foreach (var dn in dns)
        {
            if (resolved.TryGetValue(dn, out var found))
            {
                var name = GetSingleProperty(found, "sAMAccountName");
                if (!string.IsNullOrEmpty(name)) names.Add(name);
            }
        }
        return names;
    }

    // Group member rows need a human-readable label (displayName/email), not just the
    // sAMAccountName - which, in this forest, is a numeric Workday ID for some accounts.
    private List<AdGroupMember> ResolveGroupMembers(DirectoryEntry root, SearchResult result, string propertyName)
    {
        var dns = new List<string>();
        foreach (var value in result.Properties[propertyName])
        {
            var dn = value?.ToString();
            if (!string.IsNullOrEmpty(dn)) dns.Add(dn);
        }
        if (dns.Count == 0) return new List<AdGroupMember>();

        var resolved = BatchFindByDistinguishedName(root, dns, ["sAMAccountName", "displayName", "mail"]);
        var members = new List<AdGroupMember>();
        foreach (var dn in dns)
        {
            if (!resolved.TryGetValue(dn, out var found)) continue;
            var username = GetSingleProperty(found, "sAMAccountName");
            if (string.IsNullOrEmpty(username)) continue;
            members.Add(new AdGroupMember
            {
                Username = username,
                DisplayName = GetSingleProperty(found, "displayName"),
                Email = GetSingleProperty(found, "mail")
            });
        }
        return members;
    }

    private async Task<(ServiceAccountSecret account, string searchBase)> ConnectAsync(string scope, CancellationToken ct)
    {
        var account = await _secrets.GetServiceAccountAsync(ct);
        var searchBase = string.Equals(scope, "domain", StringComparison.OrdinalIgnoreCase)
            ? account.BaseDN
            : account.OuBaseDN;
        return (account, searchBase);
    }

    private static DirectoryEntry CreateDirectoryEntry(ServiceAccountSecret account, string searchBase) =>
        new(
            $"LDAP://{account.Server}/{searchBase}",
            $"{account.Domain}\\{account.Username}",
            account.Password,
            AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing);

    private static DirectoryEntry BindToDn(ServiceAccountSecret account, string dn) =>
        new(
            $"LDAP://{account.Server}/{dn}",
            $"{account.Domain}\\{account.Username}",
            account.Password,
            AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing);

    public async Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync("domain", ct);
        try
        {
            // Bind as the caller, not the service account: a successful bind IS the credential
            // check. Touching NativeObject is what forces the bind; constructing a
            // DirectoryEntry is lazy and never contacts the DC on its own.
            using var entry = new DirectoryEntry(
                $"LDAP://{account.Server}/{searchBase}",
                $"{account.Domain}\\{username}",
                password,
                AuthenticationTypes.Secure | AuthenticationTypes.Signing | AuthenticationTypes.Sealing);
            _ = entry.NativeObject;
            return true;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            // DirectoryServicesCOMException derives from COMException, so this covers both
            // an outright bad-credentials rejection and a refused bind.
            _logger.LogDebug(ex, "Credential validation bind failed.");
            return false;
        }
    }

    private static string EscapeLdapFilterValue(string value) =>
        value
            .Replace("\\", "\\5c")
            .Replace("*", "\\2a")
            .Replace("(", "\\28")
            .Replace(")", "\\29")
            .Replace("\0", "\\00");

    private static string? GetSingleProperty(SearchResult? result, string name)
    {
        if (result is null) return null;
        var values = result.Properties[name];
        return values.Count > 0 ? values[0]?.ToString() : null;
    }

    private SearchResult? FindOneUser(DirectoryEntry root, string username)
    {
        var filter = $"(&(objectCategory=person)(objectClass=user)(sAMAccountName={EscapeLdapFilterValue(username)}))";
        using var searcher = new DirectorySearcher(root) { Filter = filter, SizeLimit = 1 };
        searcher.PropertiesToLoad.AddRange(UserAttributes);
        return TimedFind("FindOneUser", () => searcher.FindOne());
    }

    private SearchResult? FindOneGroup(DirectoryEntry root, string groupName)
    {
        var filter = $"(&(objectCategory=group)(sAMAccountName={EscapeLdapFilterValue(groupName)}))";
        using var searcher = new DirectorySearcher(root) { Filter = filter, SizeLimit = 1 };
        searcher.PropertiesToLoad.AddRange(GroupAttributes);
        return TimedFind("FindOneGroup", () => searcher.FindOne());
    }

    private string FindUserDn(DirectoryEntry root, string username) =>
        GetSingleProperty(FindOneUser(root, username), "distinguishedName")
            ?? throw new InvalidOperationException($"User '{username}' was not found.");

    private string FindGroupDn(DirectoryEntry root, string groupName) =>
        GetSingleProperty(FindOneGroup(root, groupName), "distinguishedName")
            ?? throw new InvalidOperationException($"Group '{groupName}' was not found.");

    public async Task<AdUser?> GetUserAsync(string username, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var found = FindOneUser(root, username);
        return found is null ? null : MapUser(root, found);
    }

    public async Task<IReadOnlyList<AdUser>> SearchUsersAsync(string query, int limit, string scope, bool includeGroups = true, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var entry = CreateDirectoryEntry(account, searchBase);

        var escaped = EscapeLdapFilterValue(query);
        // An empty query means "match all users" - (attr=**) is an invalid LDAP
        // wildcard (adjacent asterisks), so collapse to a single (attr=*) instead.
        var filter = string.IsNullOrEmpty(escaped)
            ? "(&(objectCategory=person)(objectClass=user)(sAMAccountName=*))"
            : $"(&(objectCategory=person)(objectClass=user)(|(sAMAccountName=*{escaped}*)(mail=*{escaped}*)(displayName=*{escaped}*)(givenName=*{escaped}*)(sn=*{escaped}*)))";
        using var searcher = new DirectorySearcher(entry) { Filter = filter, SizeLimit = limit, PageSize = limit };
        searcher.PropertiesToLoad.AddRange(UserAttributes);

        var results = new List<AdUser>();
        using var found = TimedFind("SearchUsers", () => searcher.FindAll());
        foreach (SearchResult result in found)
        {
            if (results.Count >= limit) break;
            // Map straight from the search result: re-fetching each user by identity would
            // cost one extra LDAP round-trip per row for data already in hand.
            if (string.IsNullOrEmpty(GetSingleProperty(result, "sAMAccountName"))) continue;
            results.Add(MapUser(entry, result, includeGroups));
        }
        return results;
    }

    public async Task EnableUserAsync(string username, string scope, CancellationToken ct = default) =>
        await SetAccountEnabledAsync(username, scope, true, ct);

    public async Task DisableUserAsync(string username, string scope, CancellationToken ct = default) =>
        await SetAccountEnabledAsync(username, scope, false, ct);

    private async Task SetAccountEnabledAsync(string username, string scope, bool enabled, CancellationToken ct)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindUserDn(root, username);

        using var user = BindToDn(account, dn);
        var current = Convert.ToInt32(user.Properties["userAccountControl"].Value ?? 0);
        var updated = enabled ? current & ~AccountDisabledFlag : current | AccountDisabledFlag;
        if (updated == current) return;

        user.Properties["userAccountControl"].Value = updated;
        user.CommitChanges();
    }

    public async Task UnlockUserAsync(string username, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindUserDn(root, username);

        // Writing 0 to lockoutTime is the raw-LDAP unlock: AD treats it as "not locked out".
        using var user = BindToDn(account, dn);
        user.Properties["lockoutTime"].Value = 0;
        user.CommitChanges();
    }

    public async Task ResetPasswordAsync(string username, string newPassword, bool requireChange, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindUserDn(root, username);
        using var user = BindToDn(account, dn);

        // SetPassword commits to AD immediately, unlike the property write below which only
        // applies on CommitChanges(). If the pwdLastSet update throws after this line, the
        // password has already changed on the server even though the caller sees a failure -
        // wrap the follow-up steps so a partial failure still surfaces the new password that
        // is now actually in effect, instead of losing it silently.
        user.Invoke("SetPassword", [newPassword]);
        try
        {
            // pwdLastSet = 0 forces a change at next logon; -1 stamps it as "just changed".
            user.Properties["pwdLastSet"].Value = requireChange ? 0 : -1;
            user.CommitChanges();
        }
        catch (Exception ex)
        {
            throw new PasswordSetButFollowUpFailedException(newPassword, ex);
        }
    }

    public async Task ResetPasswordViaLdapsAsync(string username, string newPassword, string scope, CancellationToken ct = default)
    {
        // The target domain controllers do not serve LDAPS/StartTLS
        // -- only plain LDAP/389
        // with Kerberos sign+seal works, same as RSAT. Use the same authenticated,
        // encrypted-in-transit channel as ResetPasswordAsync instead of raw LDAPS/636.
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindUserDn(root, username);
        using var user = BindToDn(account, dn);
        user.Invoke("SetPassword", [newPassword]);
    }

    public async Task<AdGroup?> GetGroupAsync(string groupName, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var found = FindOneGroup(root, groupName);
        return found is null ? null : MapGroup(root, found);
    }

    public async Task<IReadOnlyList<AdGroup>> SearchGroupsAsync(string query, int limit, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var entry = CreateDirectoryEntry(account, searchBase);

        var filter = $"(&(objectCategory=group)(sAMAccountName=*{EscapeLdapFilterValue(query)}*))";
        using var searcher = new DirectorySearcher(entry) { Filter = filter, SizeLimit = limit, PageSize = limit };
        searcher.PropertiesToLoad.AddRange(GroupAttributes);

        var results = new List<AdGroup>();
        using var found = TimedFind("SearchGroups", () => searcher.FindAll());
        foreach (SearchResult result in found)
        {
            if (results.Count >= limit) break;
            results.Add(MapGroup(entry, result));
        }
        return results;
    }

    public async Task CreateGroupAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        if (FindOneGroup(root, name) is not null)
        {
            throw new InvalidOperationException($"Group '{name}' already exists.");
        }

        using var group = root.Children.Add($"CN={name}", "group");
        group.Properties["groupType"].Value = -2147483646;
        group.Properties["sAMAccountName"].Value = name;
        if (!string.IsNullOrEmpty(description))
        {
            group.Properties["description"].Value = description;
        }
        group.CommitChanges();
    }

    public async Task DeleteGroupAsync(string name, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindGroupDn(root, name);
        using var group = BindToDn(account, dn);
        group.DeleteTree();
    }

    public async Task SetGroupDescriptionAsync(string name, string? description, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = FindGroupDn(root, name);
        using var group = BindToDn(account, dn);
        if (string.IsNullOrEmpty(description))
        {
            group.Properties["description"].Clear();
        }
        else
        {
            group.Properties["description"].Value = description;
        }
        group.CommitChanges();
    }

    public async Task<IReadOnlyList<string>> GetGroupMembershipAsync(string username, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var dn = GetSingleProperty(FindOneUser(root, username), "distinguishedName");
        if (string.IsNullOrEmpty(dn)) return Array.Empty<string>();

        // memberOf lists only direct memberships; tokenGroups resolves the security token
        // AD itself computes, which includes nested (transitive) group membership.
        using var directoryEntry = BindToDn(account, dn);
        directoryEntry.RefreshCache(["tokenGroups"]);

        var sids = new List<byte[]>();
        foreach (byte[] sid in directoryEntry.Properties["tokenGroups"])
        {
            sids.Add(sid);
        }

        var resolved = BatchFindByObjectSid(root, sids, ["sAMAccountName"]);
        return resolved.Keys.ToList();
    }

    public async Task AddUserToGroupsAsync(string username, IReadOnlyList<string> groupNames, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var userDn = FindUserDn(root, username);

        foreach (var groupName in groupNames)
        {
            var groupDn = FindGroupDn(root, groupName);
            if (IsMember(root, groupName, userDn)) continue;

            using var group = BindToDn(account, groupDn);
            group.Properties["member"].Add(userDn);
            group.CommitChanges();
        }
    }

    public async Task RemoveUserFromGroupAsync(string username, string groupName, string scope, CancellationToken ct = default)
    {
        var (account, searchBase) = await ConnectAsync(scope, ct);
        using var root = CreateDirectoryEntry(account, searchBase);
        var userDn = FindUserDn(root, username);
        var groupDn = FindGroupDn(root, groupName);
        if (!IsMember(root, groupName, userDn)) return;

        using var group = BindToDn(account, groupDn);
        group.Properties["member"].Remove(userDn);
        group.CommitChanges();
    }

    // Let the DC evaluate membership: a server-side match on the member attribute avoids
    // pulling a potentially large member list back just to scan it locally.
    private bool IsMember(DirectoryEntry root, string groupName, string userDn)
    {
        var filter = $"(&(objectCategory=group)(sAMAccountName={EscapeLdapFilterValue(groupName)})(member={EscapeLdapFilterValue(userDn)}))";
        using var searcher = new DirectorySearcher(root) { Filter = filter, SizeLimit = 1 };
        searcher.PropertiesToLoad.Add("distinguishedName");
        return TimedFind("IsMember", () => searcher.FindOne()) is not null;
    }

    public async Task<bool> CanBindAsync(CancellationToken ct = default)
    {
        try
        {
            var (account, searchBase) = await ConnectAsync("domain", ct);
            using var entry = CreateDirectoryEntry(account, searchBase);
            using var searcher = new DirectorySearcher(entry) { Filter = "(objectClass=domain)", SizeLimit = 1 };
            _ = searcher.FindOne();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Active Directory bind check failed.");
            return false;
        }
    }

    private AdUser MapUser(DirectoryEntry root, SearchResult result, bool includeGroups = true)
    {
        var uac = int.TryParse(GetSingleProperty(result, "userAccountControl"), out var parsed) ? parsed : 0;
        var lockoutTime = GetSingleProperty(result, "lockoutTime");

        return new AdUser
        {
            Username = GetSingleProperty(result, "sAMAccountName") ?? string.Empty,
            DisplayName = GetSingleProperty(result, "displayName"),
            FirstName = GetSingleProperty(result, "givenName"),
            LastName = GetSingleProperty(result, "sn"),
            Email = GetSingleProperty(result, "mail"),
            Enabled = (uac & AccountDisabledFlag) == 0,
            IsLockedOut = !string.IsNullOrEmpty(lockoutTime) && lockoutTime != "0",
            DistinguishedName = GetSingleProperty(result, "distinguishedName"),
            Description = GetSingleProperty(result, "description"),
            Groups = includeGroups ? ResolveSamAccountNames(root, result, "memberOf") : new List<string>()
        };
    }

    private AdGroup MapGroup(DirectoryEntry root, SearchResult result)
    {
        var members = ResolveGroupMembers(root, result, "member");
        return new AdGroup
        {
            Name = GetSingleProperty(result, "sAMAccountName") ?? string.Empty,
            Description = GetSingleProperty(result, "description"),
            DistinguishedName = GetSingleProperty(result, "distinguishedName"),
            Members = members,
            MemberCount = members.Count
        };
    }
}

internal static class ActiveDirectoryGatewayTestHooks
{
    public static List<List<string>> ChunkIdentifiers(IReadOnlyList<string> identifiers, int batchSize) =>
        ActiveDirectoryGateway.ChunkIdentifiers(identifiers, batchSize);
}
