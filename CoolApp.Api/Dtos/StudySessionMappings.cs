using CoolApp.Models;

namespace CoolApp.Dtos;

// Turns entities into DTOs and back, in one place. Written by hand: a few lines per type,
// and the compiler points at every line that needs changing when a property is added.
// (Libraries like Mapperly generate this code; AutoMapper does it with reflection at run time.)
public static class StudySessionMappings
{
    public static StudySessionDto ToDto(this StudySession session) => new(
        session.Id,
        session.Course,
        session.Topic,
        session.Location,
        session.StartsAt,
        session.HostName,
        session.SeatsAvailable,
        session.Status,
        session.HostRating,
        session.Version);

    public static StudySession ToEntity(this CreateStudySessionDto dto) => new()
    {
        Course = dto.Course,
        Topic = dto.Topic,
        Location = dto.Location,
        StartsAt = dto.StartsAt,
        HostName = dto.HostName,
        SeatsAvailable = dto.SeatsAvailable
    };

    // The more specific overload wins for an UpdateStudySessionDto, so the Version comes along.
    public static StudySession ToEntity(this UpdateStudySessionDto dto)
    {
        var session = ((CreateStudySessionDto)dto).ToEntity();
        session.Version = dto.Version;
        return session;
    }
}
