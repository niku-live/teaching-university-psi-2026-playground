using System.ComponentModel.DataAnnotations;

namespace CoolApp.Models;

public class StudySession : IValidatableObject
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Course { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    [Required, StringLength(100)]
    public string HostName { get; set; } = string.Empty;

    [Range(1, 100)]
    public int SeatsAvailable { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Starts at must be in the future.",
                new[] { nameof(StartsAt) });
        }
    }
}
