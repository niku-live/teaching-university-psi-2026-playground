using CoolApp.Data;
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Repositories;

// The Entity Framework implementation of the same IStudySessionRepository contract the
// in-memory one honors - which is why nothing outside Program.cs had to change.
//
// Scoped in Program.cs, because the DbContext it depends on is scoped.
public class EfStudySessionRepository(StudySpotDbContext db) : IStudySessionRepository
{
    // AsNoTracking: hand out plain copies that the DbContext forgets about. Changing one
    // changes nothing in the database - exactly the "may return a copy" case the
    // interface's contract warned about in Lecture 06. The only way to persist a change
    // is Update.
    public IReadOnlyList<StudySession> GetAll() => db.StudySessions.AsNoTracking().ToList();

    public StudySession? GetById(int id) =>
        db.StudySessions.AsNoTracking().FirstOrDefault(s => s.Id == id);

    public StudySession Add(StudySession session)
    {
        // The contract says Add assigns the Id. Reset whatever the client sent, so the
        // database generates it instead of trying to insert a duplicate key.
        session.Id = 0;
        db.StudySessions.Add(session);
        db.SaveChanges();
        return session;
    }

    public bool Update(StudySession session)
    {
        if (!db.StudySessions.Any(s => s.Id == session.Id))
        {
            return false;
        }

        db.StudySessions.Update(session);
        db.SaveChanges();
        return true;
    }

    // ExecuteDelete runs a single DELETE statement - no need to load the session first.
    public bool Delete(int id) => db.StudySessions.Where(s => s.Id == id).ExecuteDelete() > 0;
}
