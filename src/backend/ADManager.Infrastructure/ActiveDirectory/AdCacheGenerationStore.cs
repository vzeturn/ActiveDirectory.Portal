using System.Collections.Concurrent;

namespace ADManager.Infrastructure.ActiveDirectory;

// Tracks per-scope search-result "generations" so a write can invalidate every
// outstanding SearchUsersAsync/SearchGroupsAsync cache entry for that scope
// without needing to enumerate their arbitrary query/limit-keyed cache keys.
public sealed class AdCacheGenerationStore
{
    private readonly ConcurrentDictionary<string, int> _generations = new();

    public int GetGeneration(string scope, string kind) =>
        _generations.GetOrAdd(Key(scope, kind), 0);

    public void BumpGeneration(string scope, string kind) =>
        _generations.AddOrUpdate(Key(scope, kind), 1, (_, current) => current + 1);

    private static string Key(string scope, string kind) => $"{scope}:{kind}";
}
