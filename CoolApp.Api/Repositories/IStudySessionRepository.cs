using CoolApp.Models;

namespace CoolApp.Repositories;

// The abstraction the rest of the app depends on (Dependency Inversion): callers say
// *what* they need stored and retrieved, never *where* it lives. The app uses
// EfStudySessionRepository (SQLite through Entity Framework); the unit tests use
// InMemoryStudySessionRepository. Swapping one for the other touched nothing that uses
// this interface.
//
// The contract is part of the interface, not just the signatures - every implementation
// has to honor it, or callers can't safely substitute one for another (Liskov):
//   - GetById returns null for a missing id; it never throws.
//   - GetById may hand back the stored instance *or a copy*. Callers must not rely on
//     mutating what they got back - the only way to persist a change is Update.
//   - Update and Delete return false (and change nothing) when the id doesn't exist.
//   - Update throws StudySessionConflictException (and changes nothing) when the session
//     was changed since the caller read it - session.Version is the version they read.
//     An implementation that can't tell (InMemoryStudySessionRepository) never throws it.
//   - A cancelled token stops the work: the method throws OperationCanceledException
//     instead of reading or changing anything.
//
// Every method is async: storage is I/O (a database, a file, a network call), and a
// thread shouldn't sit blocked while it waits. The CancellationToken is required, not
// `= default`, so the compiler makes every caller pass one along - an optional token is
// easy to forget, and a forgotten token cancels nothing.
public interface IStudySessionRepository
{
    Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken);

    Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken);

    // Assigns the new session's Id and returns the stored session.
    Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
