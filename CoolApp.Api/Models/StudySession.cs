using System.ComponentModel.DataAnnotations;

namespace CoolApp.Models;

// The entity: what the database stores (Entity Framework reads the [Required]/[StringLength]
// attributes for the table's columns). Since Lecture 10 the API no longer binds or returns it
// directly - clients send CreateStudySessionDto/UpdateStudySessionDto and get StudySessionDto back.
public class StudySession : IComparable<StudySession>
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Course { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    public DateTimeOffset StartsAt { get; set; }

    [Required, StringLength(100)]
    public string HostName { get; set; } = string.Empty;

    // 0 is a valid, if unusual, input: a host can list a session as already full
    // (e.g. an informal group that's not taking more people) even before anyone RSVPs.
    [Range(0, 100)]
    public int SeatsAvailable { get; set; }

    public SessionStatus Status => SeatsAvailable > 0 ? SessionStatus.Scheduled : SessionStatus.Full;

    // Nullable, not just because "not rated yet" is a real state (Rating? = null
    // until someone rates it), but because Rating's own constructor would throw if
    // asked to represent "no rating" as some sentinel int (e.g. 0) instead.
    //
    // Only ever set server-side, via RateSession's already-validated RatingSubmission:
    // the create/update DTOs have no HostRating for a client to fill in.
    public Rating? HostRating { get; set; }

    // Optimistic concurrency: [ConcurrencyCheck] makes EF add the version it read to the
    // WHERE of every UPDATE. EfStudySessionRepository raises it by one on every save, so
    // if someone else saved the session since it was read, no row matches and
    // SaveChanges throws DbUpdateConcurrencyException instead of overwriting their change.
    //
    // A client sends back the Version it read (GET) with its change (PUT).
    [ConcurrencyCheck]
    public int Version { get; set; }

    // Lets sessions sort by start time via List<T>.Sort() or Array.Sort(), with
    // no comparer to pass in - the type itself defines what "in order" means.
    //
    // IComparable<T>.CompareTo's contract only cares about the *sign* of the
    // result, never its exact magnitude: negative means "this instance sorts
    // before other," zero means "equal for sorting purposes," positive means
    // "this instance sorts after other." Written with explicit branches
    // returning -1/0/1 (rather than the one-liner `StartsAt.CompareTo(other.StartsAt)`)
    // so that mapping is visible here, not hidden inside DateTimeOffset's own
    // CompareTo.
    public int CompareTo(StudySession? other)
    {
        if (other is null)
        {
            // By convention, any non-null instance sorts after a null one.
            return 1;
        }

        if (StartsAt < other.StartsAt)
        {
            return -1;
        }

        if (StartsAt > other.StartsAt)
        {
            return 1;
        }

        return 0;
    }
}
