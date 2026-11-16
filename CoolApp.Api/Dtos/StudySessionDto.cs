using CoolApp.Models;

namespace CoolApp.Dtos;

// What the API sends back for a study session: the response contract. It's a copy of the
// entity's values, so the entity can change (a new column, a navigation property, something
// only the server needs) without every client seeing it, and the JSON stays the same when it does.
public record StudySessionDto(
    int Id,
    string Course,
    string Topic,
    string Location,
    DateTimeOffset StartsAt,
    string HostName,
    int SeatsAvailable,
    SessionStatus Status,
    Rating? HostRating,
    int Version);
