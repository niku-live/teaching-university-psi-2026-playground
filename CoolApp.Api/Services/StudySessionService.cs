using CoolApp.Extensions;
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Services;

// The rules of the app (what counts as "upcoming", how sessions are filtered and
// ordered, how a rating is applied) live here - not in the controller, which only
// translates HTTP to and from these calls (Single Responsibility), and not in the
// repository, which only stores and retrieves.
//
// The repository arrives through the constructor (Dependency Injection) instead of
// being created here, so this class doesn't know or care where sessions are stored.
public class StudySessionService(IStudySessionRepository repository) : IStudySessionService
{
    public async Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken)
    {
        // asOf: a real (not just documented) use of UpcomingOnly's optional argument -
        // omit it for "upcoming as of right now" (every other call site does this), or
        // pass it to preview what the list will look like at a future moment.
        //
        // course/minSeatsAvailable are supplied in Filter's own declared order here, so
        // naming them is a readability choice, not a requirement - contrast GetSummaries
        // below, which skips the first one entirely and *must* name the second.
        //
        // LINQ (Where, inside UpcomingOnly/Filter) filters; IComparable<StudySession>
        // (via CompareTo) lets List<T>.Sort() put what's left in start-time order with
        // no comparer.
        //
        // The await comes first: the filtering and sorting below run in memory, on the
        // list the repository already loaded.
        var sessions = await repository.GetAllAsync(cancellationToken);
        var upcoming = sessions
            .UpcomingOnly(asOf: asOf)
            .Filter(course, minSeatsAvailable)
            .ToList();
        upcoming.Sort();
        return upcoming;
    }

    public async Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken)
    {
        // This endpoint deliberately never exposes a course filter - only minSeatsAvailable.
        // Filter(minSeatsAvailable: minSeatsAvailable) skips `course` (Filter's first
        // parameter) entirely: there is no positional way to write that call. Naming
        // isn't optional polish here, it's the only way to reach the parameter you want
        // without also having to know and restate the one you don't.
        var sessions = await repository.GetAllAsync(cancellationToken);
        return sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
            .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));
    }

    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken) =>
        repository.AddAsync(session, cancellationToken);

    public async Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.Course = session.Course;
        existing.Topic = session.Topic;
        existing.Location = session.Location;
        existing.StartsAt = session.StartsAt;
        existing.HostName = session.HostName;
        existing.SeatsAvailable = session.SeatsAvailable;

        // Without this line the in-memory repository would still appear to work, because
        // GetById happens to return the stored instance. A repository that returns copies
        // (like a database would) would silently lose the change - see the contract on
        // IStudySessionRepository.
        return await repository.UpdateAsync(existing, cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);

    public async Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        // [Range] on RatingSubmission.Value already rejected anything outside 1-5
        // before this line runs - the Rating constructor re-checks anyway, since a
        // Rating that's out of range has to be impossible everywhere, not just here.
        existing.HostRating = new Rating(value);
        return await repository.UpdateAsync(existing, cancellationToken);
    }
}
