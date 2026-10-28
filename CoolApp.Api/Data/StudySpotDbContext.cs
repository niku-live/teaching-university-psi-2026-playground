using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The Entity Framework "unit of work": one instance per request (AddDbContext registers
// it as scoped), tracking the entities it loaded and turning SaveChanges into SQL.
//
// Only the repository uses it. The service and the controller never see it, so the
// rest of the app doesn't know (or care) that sessions now live in a database.
public class StudySpotDbContext(DbContextOptions<StudySpotDbContext> options) : DbContext(options)
{
    public DbSet<StudySession> StudySessions => Set<StudySession>();

    // Only what conventions cannot guess has to be configured. Id becomes the primary
    // key, [Required] becomes NOT NULL (SQLite ignores the [StringLength] lengths, but
    // a real server database would use them for the column types), and
    // Status isn't mapped at all: it only has a getter, so it's calculated every time.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<StudySession>();

        // Rating is our own struct, not a type a database column can hold - store its
        // int Value, and go back through the Rating constructor (and its 1-5 check) when
        // reading. "Not rated yet" stays NULL.
        session.Property(s => s.HostRating)
            .HasConversion(rating => rating!.Value.Value, value => new Rating(value));
    }

    // The same configuration for the application and the `dotnet ef` tool.
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseSqlite(connectionString)
            // Runs after migrations are applied (by Migrate or `dotnet ef database update`),
            // every time - so it has to check whether the data is already there.
            .UseSeeding((context, _) =>
            {
                if (!context.Set<StudySession>().Any())
                {
                    context.Set<StudySession>().AddRange(SampleSessions());
                    context.SaveChanges();
                }
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                if (!await context.Set<StudySession>().AnyAsync(cancellationToken))
                {
                    context.Set<StudySession>().AddRange(SampleSessions());
                    await context.SaveChangesAsync(cancellationToken);
                }
            });

    // The same two sessions InMemoryStudySessionRepository starts with. Not HasData (part
    // of a migration), because they start "tomorrow" relative to the first run.
    private static IEnumerable<StudySession> SampleSessions() =>
    [
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
        }
    ];
}
