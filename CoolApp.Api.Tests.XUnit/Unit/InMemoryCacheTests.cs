using CoolApp.Caching;

namespace CoolApp.Api.Tests.XUnit.Unit;

// The generic cache with two different type arguments - the same code, checked by the
// compiler for each: InMemoryCache<string, string> and InMemoryCache<int, object>.
public class InMemoryCacheTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetOrAdd_SameKeyTwice_LoadsOnce()
    {
        var cache = new InMemoryCache<string, string>();
        var loads = 0;
        Task<string> Load(string key, CancellationToken _)
        {
            loads++;
            return Task.FromResult(key.ToUpperInvariant());
        }

        await cache.GetOrAddAsync("psi", Load, Cancellation);
        var second = await cache.GetOrAddAsync("psi", Load, Cancellation);

        Assert.Equal("PSI", second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Remove_NextGetOrAdd_LoadsAgain()
    {
        var cache = new InMemoryCache<string, string>();
        await cache.GetOrAddAsync("psi", (_, _) => Task.FromResult("old"), Cancellation);

        cache.Remove("psi");
        var value = await cache.GetOrAddAsync("psi", (_, _) => Task.FromResult("new"), Cancellation);

        Assert.Equal("new", value);
    }

    [Fact]
    public async Task GetOrAdd_LoadReturnsNull_IsNotCached()
    {
        var cache = new InMemoryCache<int, object?>();
        await cache.GetOrAddAsync(1, (_, _) => Task.FromResult<object?>(null), Cancellation);

        var value = await cache.GetOrAddAsync(1, (_, _) => Task.FromResult<object?>("created since"), Cancellation);

        Assert.Equal("created since", value);
    }

    // The shared memory scenario: 100 requests ask for the same key at the same time, on
    // thread pool threads. Several of them may miss and load, but GetOrAdd keeps only the
    // first value, so all of them get the same instance back.
    //
    // `loads` is shared between those threads too: `loads++` is read-add-write, and two
    // threads can read the same old value. Interlocked.Increment does it as one step.
    [Fact]
    public async Task GetOrAdd_ManyRequestsAtOnce_AllGetTheSameValue()
    {
        var cache = new InMemoryCache<int, object>();
        var loads = 0;
        async Task<object> Load(int key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref loads);
            await Task.Delay(10, cancellationToken);
            return new object();
        }

        var values = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => cache.GetOrAddAsync(1, Load, Cancellation), Cancellation)));

        Assert.All(values, value => Assert.Same(values[0], value));
        Assert.InRange(loads, 1, values.Length);
    }
}
