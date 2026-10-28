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

    public async Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _inner.GetAllAsync(cancellationToken)).Select(Copy).ToList();

    public async Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _inner.GetByIdAsync(id, cancellationToken) is { } s ? Copy(s) : null;

    public async Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken) =>
        Copy(await _inner.AddAsync(Copy(session), cancellationToken));

    public Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken) =>
        _inner.UpdateAsync(Copy(session), cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(id, cancellationToken);
}
