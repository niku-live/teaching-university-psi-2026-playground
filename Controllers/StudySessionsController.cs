using Microsoft.AspNetCore.Mvc;
using CoolApp.Extensions;
using CoolApp.Models;

namespace CoolApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudySessionsController : ControllerBase
{
    // In-memory sample data. A real implementation would use a database (see Lab Assignment #2).
    private static readonly List<StudySession> Sessions = new()
    {
        new StudySession
        {
            Id = 1,
            Course = "Software Development I",
            Topic = "Pull request etiquette",
            Location = "MIF, room 401",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            HostName = "Ieva",
            SeatsAvailable = 3
        },
        new StudySession
        {
            Id = 2,
            Course = "Software Development I",
            Topic = ".NET memory model (stack vs heap)",
            Location = "Library, 2nd floor",
            StartsAt = DateTimeOffset.UtcNow.AddDays(2),
            HostName = "Tomas",
            SeatsAvailable = 5
        }
    };

    [HttpGet]
    public IEnumerable<StudySession> GetAll(
        [FromQuery] string? course = null,
        [FromQuery] int? minSeatsAvailable = null,
        [FromQuery] DateTimeOffset? asOf = null)
    {
        // asOf: a real (not just documented) use of UpcomingOnly's optional argument -
        // omit it for "upcoming as of right now" (every other call site does this), or
        // pass ?asOf=... to preview what the list will look like at a future moment.
        //
        // course/minSeatsAvailable are supplied in Filter's own declared order here, so
        // naming them is a readability choice, not a requirement - contrast GetSummaries
        // below, which skips the first one entirely and *must* name the second.
        //
        // LINQ (Where, inside UpcomingOnly/Filter) filters; IComparable<StudySession>
        // (via CompareTo) lets List<T>.Sort() put what's left in start-time order with
        // no comparer.
        var upcoming = Sessions
            .UpcomingOnly(asOf: asOf)
            .Filter(course, minSeatsAvailable)
            .ToList();
        upcoming.Sort();
        return upcoming;
    }

    [HttpGet("summary")]
    public IEnumerable<StudySessionSummary> GetSummaries([FromQuery] int? minSeatsAvailable = null) =>
        // This endpoint deliberately never exposes a course filter - only minSeatsAvailable.
        // Filter(minSeatsAvailable: minSeatsAvailable) skips `course` (Filter's first
        // parameter) entirely: there is no positional way to write that call. Naming
        // isn't optional polish here, it's the only way to reach the parameter you want
        // without also having to know and restate the one you don't.
        Sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
            .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));

    [HttpGet("{id:int}")]
    public ActionResult<StudySession> GetById(int id)
    {
        var session = Sessions.FirstOrDefault(s => s.Id == id);
        return session is null ? NotFound() : session;
    }

    [HttpPost]
    public ActionResult<StudySession> Create(StudySession session)
    {
        session.Id = Sessions.Count == 0 ? 1 : Sessions.Max(s => s.Id) + 1;
        Sessions.Add(session);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, StudySession session)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Course = session.Course;
        existing.Topic = session.Topic;
        existing.Location = session.Location;
        existing.StartsAt = session.StartsAt;
        existing.HostName = session.HostName;
        existing.SeatsAvailable = session.SeatsAvailable;

        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        Sessions.Remove(existing);
        return NoContent();
    }

    [HttpPut("{id:int}/rating")]
    public IActionResult RateSession(int id, RatingSubmission submission)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        // [Range] on RatingSubmission.Value already rejected anything outside 1-5
        // before this line runs - the Rating constructor re-checks anyway, since a
        // Rating that's out of range has to be impossible everywhere, not just here.
        existing.HostRating = new Rating(submission.Value);
        return NoContent();
    }
}
