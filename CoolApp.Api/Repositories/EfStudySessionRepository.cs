using CoolApp.Data;
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Repositories;

// The Entity Framework implementation of the same IStudySessionRepository contract the
// in-memory one honors - which is why nothing outside Program.cs had to change.
//
// Scoped in Program.cs, because the DbContext it depends on is scoped.
//
// Every query uses EF's async version (ToListAsync, SaveChangesAsync, ...) and passes the
// CancellationToken on: if the client gives up on the request, the database query is
// cancelled too instead of running to completion for nobody.
public class EfStudySessionRepository(StudySpotDbContext db) : IStudySessionRepository
{
    // AsNoTracking: hand out plain copies that the DbContext forgets about. Changing one
    // changes nothing in the database - exactly the "may return a copy" case the
    // interface's contract warned about in Lecture 06. The only way to persist a change
    // is Update.
    public async Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.StudySessions.AsNoTracking().ToListAsync(cancellationToken);

    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.StudySessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken)
    {
        // The contract says Add assigns the Id. Reset whatever the client sent, so the
        // database generates it instead of trying to insert a duplicate key.
        session.Id = 0;
        db.StudySessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken)
    {
        if (!await db.StudySessions.AnyAsync(s => s.Id == session.Id, cancellationToken))
        {
            return false;
        }

        // Update attaches the (untracked) session and marks every column as changed.
        db.StudySessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ExecuteDeleteAsync runs a single DELETE statement - no need to load the session first.
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.StudySessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
