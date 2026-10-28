using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The Entity Framework "unit of work": one instance per request (AddDbContext registers
// it as scoped), tracking the entities it loaded and turning SaveChanges() into SQL.
//
// Only the repository uses it. The service and the controller never see it, so the
// rest of the app doesn't know (or care) that sessions now live in a database.
public class StudySpotDbContext(DbContextOptions<StudySpotDbContext> options) : DbContext(options)
{
    public DbSet<StudySession> StudySessions => Set<StudySession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Rating is our own struct, not a type a database column can hold - store its
        // int Value, and go back through the Rating constructor (and its 1-5 check) when
        // reading. EF applies a converter for Rating to the nullable Rating? property
        // too, and keeps "not rated yet" as NULL.
        //
        // Status needs no configuration: it only has a getter, so EF doesn't map it -
        // it's calculated from SeatsAvailable every time, never stored.
        modelBuilder.Entity<StudySession>()
            .Property(s => s.HostRating)
            .HasConversion(rating => rating!.Value.Value, value => new Rating(value));
    }
}
