using Microsoft.AspNetCore.Mvc;
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Controllers;

// Only translates HTTP to and from IStudySessionService calls: routing, model binding,
// status codes. Everything about what the app actually *does* lives in the service.
[ApiController]
[Route("api/[controller]")]
public class StudySessionsController(IStudySessionService service) : ControllerBase
{
    [HttpGet]
    public IEnumerable<StudySession> GetAll(
        [FromQuery] string? course = null,
        [FromQuery] int? minSeatsAvailable = null,
        [FromQuery] DateTimeOffset? asOf = null) =>
        service.GetUpcoming(course, minSeatsAvailable, asOf);

    [HttpGet("summary")]
    public IEnumerable<StudySessionSummary> GetSummaries([FromQuery] int? minSeatsAvailable = null) =>
        service.GetSummaries(minSeatsAvailable);

    [HttpGet("{id:int}")]
    public ActionResult<StudySession> GetById(int id)
    {
        var session = service.GetById(id);
        return session is null ? NotFound() : session;
    }

    [HttpPost]
    public ActionResult<StudySession> Create(StudySession session)
    {
        var created = service.Create(session);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public IActionResult Update(int id, StudySession session) =>
        service.Update(id, session) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id) =>
        service.Delete(id) ? NoContent() : NotFound();

    [HttpPut("{id:int}/rating")]
    public IActionResult RateSession(int id, RatingSubmission submission) =>
        service.Rate(id, submission.Value) ? NoContent() : NotFound();
}
