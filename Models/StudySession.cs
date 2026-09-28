using System.ComponentModel.DataAnnotations;

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

    // Lets sessions sort by start time via List<T>.Sort() or Array.Sort(), with
    // no comparer to pass in - the type itself defines what "in order" means.
    public int CompareTo(StudySession? other) => StartsAt.CompareTo(other?.StartsAt ?? default);

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
