using CoolApp.Models;

namespace CoolApp.Services;

// What the controller needs from the app's rules, without knowing which class provides
// them (Dependency Inversion again, one layer up): the controller depends on this
// interface, so a test can hand it a fake service and check what the controller does
// with the results - status codes, 404s - without any real data or rules involved.
//
// "Not found" is reported as null/false rather than an exception, so the controller can
// turn it into a 404 without a try/catch.
//
// Async all the way up: the repository is async, so everything that calls it is too, and
// each method passes the caller's CancellationToken on to the repository.
public interface IStudySessionService
{
    Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken);

    Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken);

    Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);

    Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken);
}
