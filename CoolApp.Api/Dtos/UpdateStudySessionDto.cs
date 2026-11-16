namespace CoolApp.Dtos;

// An update carries everything a create does, plus the Version the client read (GET), so
// the repository can tell whether someone else saved the session in between (Lecture 09's 409).
public record UpdateStudySessionDto : CreateStudySessionDto
{
    public int Version { get; init; }
}
