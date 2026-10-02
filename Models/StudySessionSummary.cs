namespace CoolApp.Models;

// An immutable, read-only projection of a StudySession - no Id, no HostName,
// just what a listing view actually needs. Two summaries are equal if all
// their values match, with no extra code required to get that behavior.
//
// HostRating is read-only here on purpose: this record has no Id to act
// against, so it can show a rating but never be the target of one - that
// still happens through StudySession itself (see StudySessions.js's table).
public record StudySessionSummary(string Course, string Topic, DateTimeOffset StartsAt, int SeatsAvailable, Rating? HostRating);
