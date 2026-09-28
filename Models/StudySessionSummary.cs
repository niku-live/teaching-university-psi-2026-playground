namespace CoolApp.Models;

// An immutable, read-only projection of a StudySession - no Id, no HostName,
// just what a listing view actually needs. Two summaries are equal if all
// their values match, with no extra code required to get that behavior.
public record StudySessionSummary(string Course, string Topic, DateTimeOffset StartsAt, int SeatsAvailable);
