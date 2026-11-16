using System.ComponentModel.DataAnnotations;

namespace CoolApp.Dtos;

// What a client sends to create a study session: the request contract. Only the fields a
// client is allowed to choose - no Id, Version or HostRating. Binding the entity itself let a
// client "over-post" those: a POST with "version": 50 was stored as sent. (The Id was only safe
// because EfStudySessionRepository.AddAsync happens to reset it; every new entity property
// would have needed a guard like that.)
//
// Validation lives here, at the trust boundary (theory 09: invalid input from outside is
// expected, so it's rejected with a 400 the client can act on).
public record CreateStudySessionDto : IValidatableObject
{
    [Required, StringLength(100)]
    public string Course { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Topic { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; init; } = string.Empty;

    public DateTimeOffset StartsAt { get; init; }

    [Required, StringLength(100)]
    public string HostName { get; init; } = string.Empty;

    // 0 is a valid, if unusual, input: a host can list a session as already full.
    [Range(0, 100)]
    public int SeatsAvailable { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt <= DateTimeOffset.UtcNow)
        {
            yield return new ValidationResult("Starts at must be in the future.", [nameof(StartsAt)]);
        }
    }
}
