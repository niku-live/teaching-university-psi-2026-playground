using CoolApp.Models;

namespace CoolApp.Services;

// What the controller needs from the app's rules, without knowing which class provides
// them (Dependency Inversion again, one layer up): the controller depends on this
// interface, so a test can hand it a fake service and check what the controller does
// with the results - status codes, 404s - without any real data or rules involved.
//
// "Not found" is reported as null/false rather than an exception, so the controller can
// turn it into a 404 without a try/catch.
public interface IStudySessionService
{
    IEnumerable<StudySession> GetUpcoming(string? course, int? minSeatsAvailable, DateTimeOffset? asOf);

    IEnumerable<StudySessionSummary> GetSummaries(int? minSeatsAvailable);

    StudySession? GetById(int id);

    StudySession Create(StudySession session);

    bool Update(int id, StudySession session);

    bool Delete(int id);

    bool Rate(int id, int value);
}
