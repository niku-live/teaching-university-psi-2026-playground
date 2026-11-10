using CoolApp.Caching;
using CoolApp.Models;

namespace CoolApp.Services;

// A decorator: implements IStudySessionService by wrapping another IStudySessionService and
// adding one thing - caching a session by id. The controller still asks for
// IStudySessionService and doesn't know the cache exists (Open/Closed: the behavior is
// extended without editing StudySessionService).
//
// Every change goes to the real service first and then removes the session from the cache,
// so the next read loads the new version. (This cache lives in one app's memory: with
// several instances of the app, each would need to be told - a distributed cache like
// Redis solves that.)
public class CachedStudySessionService(StudySessionService inner, ICache<int, StudySession?> cache) : IStudySessionService
{
    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        cache.GetOrAddAsync(id, inner.GetByIdAsync, cancellationToken);

    public Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken) =>
        ChangeThenForgetAsync(id, () => inner.UpdateAsync(id, session, cancellationToken));

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        ChangeThenForgetAsync(id, () => inner.DeleteAsync(id, cancellationToken));

    public Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken) =>
        ChangeThenForgetAsync(id, () => inner.RateAsync(id, value, cancellationToken));

    // A generic method: runs any change, whatever type it returns, and then removes the session
    // from the cache - also when the change throws (after a 409 conflict someone else has changed
    // the session, so the cached copy is old). One copy of this try/finally instead of one per
    // change; TResult is inferred from the lambda, so the callers never write it.
    private async Task<TResult> ChangeThenForgetAsync<TResult>(int id, Func<Task<TResult>> change)
    {
        try
        {
            return await change();
        }
        finally
        {
            cache.Remove(id);
        }
    }

    // Lists and new sessions aren't cached: passed straight through.
    public Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken) =>
        inner.GetUpcomingAsync(course, minSeatsAvailable, asOf, cancellationToken);

    public Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken) =>
        inner.GetSummariesAsync(minSeatsAvailable, cancellationToken);

    public Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken) =>
        inner.CreateAsync(session, cancellationToken);
}
