using System.Collections.Concurrent;

namespace CoolApp.Caching;

// Shared memory: the app registers this as a singleton (Program.cs), so ONE instance is used
// by every request, and ASP.NET Core runs requests in parallel on different threads.
//
// A plain Dictionary<TKey, TValue> isn't safe for that: two threads writing at the same time
// can corrupt it (lost entries, an exception, or an endless loop inside it). The usual fixes
// are a `lock` around every access, or a concurrent collection - ConcurrentDictionary does
// the locking itself, per part of the dictionary, so readers don't wait for each other.
public sealed class InMemoryCache<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _items = new();

    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> load, CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await load(key, cancellationToken);
        if (loaded is null)
        {
            return loaded;
        }

        // Two requests can miss the same key at the same moment and both load it. GetOrAdd
        // is atomic: the first value stored wins, and both requests get that same value back.
        // (Each check-then-act step on its own would be a race: "if not there, add" can be
        // interrupted between the check and the add.)
        return _items.GetOrAdd(key, loaded);
    }

    public void Remove(TKey key) => _items.TryRemove(key, out _);
}
