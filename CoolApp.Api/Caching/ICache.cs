namespace CoolApp.Caching;

// A generic cache: one implementation works for any key and value type, checked by the
// compiler - ICache<int, StudySession> only accepts int keys and only hands back sessions,
// with no casts from object (what a non-generic cache would need).
//
// The constraint `where TKey : notnull` says what the implementation needs from the key
// type: a dictionary can't store a null key.
//
// TKey and TValue are invariant (no `in`/`out`, theory 09): TValue is both returned
// (GetOrAddAsync) and passed in (as the result of `load`), so neither variance would be safe.
public interface ICache<TKey, TValue>
    where TKey : notnull
{
    // Returns the cached value for the key, or calls `load`, caches what it returns
    // (unless it's null - "not found" isn't cached) and returns that.
    Task<TValue> GetOrAddAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> load, CancellationToken cancellationToken);

    // Forgets the key, so the next GetOrAddAsync loads it again.
    void Remove(TKey key);
}
