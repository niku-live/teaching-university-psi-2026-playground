using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Tests.Unit;

// Fake: a second, working implementation of IStudySessionRepository that - like a real
// database - hands out copies. Mutating what GetById returned changes nothing until
// Update is called (the Liskov "gotcha" from Lecture 06, now as a test double).
public class CopyingStudySessionRepository : IStudySessionRepository
{
    private readonly InMemoryStudySessionRepository _inner = new();

    private static StudySession Copy(StudySession s) => new()
    {
        Id = s.Id, Course = s.Course, Topic = s.Topic, Location = s.Location,
        StartsAt = s.StartsAt, HostName = s.HostName, SeatsAvailable = s.SeatsAvailable,
        HostRating = s.HostRating
    };

    public IReadOnlyList<StudySession> GetAll() => _inner.GetAll().Select(Copy).ToList();
    public StudySession? GetById(int id) => _inner.GetById(id) is { } s ? Copy(s) : null;
    public StudySession Add(StudySession session) => Copy(_inner.Add(Copy(session)));
    public bool Update(StudySession session) => _inner.Update(Copy(session));
    public bool Delete(int id) => _inner.Delete(id);
}
