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

    public async Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.UpdateAsync(id, session, cancellationToken);
        }
        finally
        {
            // Also after a 409 conflict: someone else changed the session, so the cached copy is old.
            cache.Remove(id);
        }
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await inner.DeleteAsync(id, cancellationToken);
        cache.Remove(id);
        return deleted;
    }

    public async Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken)
    {
        var rated = await inner.RateAsync(id, value, cancellationToken);
        cache.Remove(id);
        return rated;
    }

    // Lists and new sessions aren't cached: passed straight through.
    public Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken) =>
        inner.GetUpcomingAsync(course, minSeatsAvailable, asOf, cancellationToken);

    public Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken) =>
        inner.GetSummariesAsync(minSeatsAvailable, cancellationToken);

    public Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken) =>
        inner.CreateAsync(session, cancellationToken);
}
