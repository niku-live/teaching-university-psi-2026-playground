using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The same two sample sessions InMemoryStudySessionRepository starts with, written to
// the database the first time the app starts with an empty table.
//
// Not a migration (HasData): migrations need fixed values, and these sessions start
// "tomorrow" and "the day after" relative to whenever you first run the app.
public static class SampleData
{
    public static async Task SeedAsync(StudySpotDbContext db)
    {
        if (await db.StudySessions.AnyAsync())
        {
            return;
        }

        db.StudySessions.AddRange(
            new StudySession
            {
                Course = "Software Development I",
                Topic = "Pull request etiquette",
                Location = "MIF, room 401",
                StartsAt = DateTimeOffset.UtcNow.AddDays(1),
                HostName = "Ieva",
                SeatsAvailable = 3
            },
            new StudySession
            {
                Course = "Software Development I",
                Topic = ".NET memory model (stack vs heap)",
                Location = "Library, 2nd floor",
                StartsAt = DateTimeOffset.UtcNow.AddDays(2),
                HostName = "Tomas",
                SeatsAvailable = 5
            });
        await db.SaveChangesAsync();
    }
}
