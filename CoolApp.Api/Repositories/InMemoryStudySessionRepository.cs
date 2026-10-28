using CoolApp.Models;

namespace CoolApp.Repositories;

// In-memory sample data. The app itself now uses EfStudySessionRepository; this one stays
// for the unit tests, which need a fast, real repository with no database behind it.
//
// If you register it in Program.cs, make it a singleton: the data lives in this
// instance's list, so a new instance per request would forget every session.
public class InMemoryStudySessionRepository : IStudySessionRepository
{
    private readonly List<StudySession> _sessions = new()
    {
        new StudySession
        {
            Id = 1,
            Course = "Software Development I",
            Topic = "Pull request etiquette",
            Location = "MIF, room 401",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            HostName = "Ieva",
            SeatsAvailable = 3
        },
        new StudySession
        {
            Id = 2,
            Course = "Software Development I",
            Topic = ".NET memory model (stack vs heap)",
            Location = "Library, 2nd floor",
            StartsAt = DateTimeOffset.UtcNow.AddDays(2),
            HostName = "Tomas",
            SeatsAvailable = 5
        }
    };

    // Nothing here does any I/O, so there is nothing to await: each method does its work
    // right away and wraps the result in an already-completed Task (Task.FromResult) to
    // honor the async interface.
    //
    // It still honors the cancellation part of the contract: a request that was already
    // cancelled gets an OperationCanceledException, exactly as a database-backed
    // implementation would give it, instead of a result nobody is waiting for.
    public Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<StudySession>>(_sessions.ToList());
    }

    // Returns the stored instance itself. That's an implementation detail, not part of
    // the contract (see IStudySessionRepository): callers still have to call Update.
    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
    }

    public Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.Id = _sessions.Count == 0 ? 1 : _sessions.Max(s => s.Id) + 1;
        _sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = _sessions.FindIndex(s => s.Id == session.Id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _sessions[index] = session;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = _sessions.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(existing is not null && _sessions.Remove(existing));
    }
}
