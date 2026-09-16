using Microsoft.AspNetCore.Mvc;
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
            StartsAt = DateTime.Now.AddDays(1),
            HostName = "Ieva",
            SeatsAvailable = 3
        },
        new StudySession
        {
            Id = 2,
            Course = "Software Development I",
            Topic = ".NET memory model (stack vs heap)",
            Location = "Library, 2nd floor",
            StartsAt = DateTime.Now.AddDays(2),
            HostName = "Tomas",
            SeatsAvailable = 5
        }
    };

    [HttpGet]
    public IEnumerable<StudySession> GetAll() => Sessions;

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
}
