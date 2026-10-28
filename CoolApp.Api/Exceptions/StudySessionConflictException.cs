namespace CoolApp.Exceptions;

// Thrown when a study session can't be saved because someone else saved it after it was
// read (see StudySession.Version). Our own type, not DbUpdateConcurrencyException: the
// service and controller don't know Entity Framework exists, and callers can catch
// exactly this case without catching every other database error with it.
public class StudySessionConflictException(int sessionId, Exception? innerException = null)
    : Exception($"Study session {sessionId} was changed by someone else after you loaded it. Load it again and reapply your change.", innerException)
{
    public int SessionId { get; } = sessionId;
}
