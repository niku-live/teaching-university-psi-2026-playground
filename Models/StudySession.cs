using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CoolApp.Models;

public class StudySession : IValidatableObject, IComparable<StudySession>
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
    // [BindNever] keeps this out of Create/Update's model binding entirely - it's
    // only ever set server-side, via RateSession's already-validated RatingSubmission,
    // not by a client just including "hostRating" in a POST/PUT body.
    [BindNever]
    public Rating? HostRating { get; set; }

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

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt <= DateTimeOffset.UtcNow)
        {
            yield return new ValidationResult(
                "Starts at must be in the future.",
                new[] { nameof(StartsAt) });
        }
    }
}
