using Microsoft.AspNetCore.Mvc;
using CoolApp.Dtos;
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Controllers;

// Only translates HTTP to and from IStudySessionService calls: routing, model binding,
// status codes. Everything about what the app actually *does* lives in the service.
//
// The HTTP contract is the DTOs (CoolApp.Dtos): requests bind to CreateStudySessionDto /
// UpdateStudySessionDto and responses are StudySessionDto. The service and repository keep
// working with the StudySession entity; the mapping happens here, at the edge.
//
// Every action is async and takes a CancellationToken. ASP.NET Core fills that parameter
// in by itself (it's HttpContext.RequestAborted): it is cancelled when the client closes
// the connection, and passing it down lets the repository (and, once there is one,
// the database) stop working on an answer nobody is waiting for.
//
// The action names stay without an "Async" suffix: MVC strips that suffix from action
// names by default, so CreatedAtAction(nameof(GetByIdAsync), ...) would point at an
// action that doesn't exist.
[ApiController]
[Route("api/[controller]")]
public class StudySessionsController(IStudySessionService service) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<StudySessionDto>> GetAll(
        CancellationToken cancellationToken,
        [FromQuery] string? course = null,
        [FromQuery] int? minSeatsAvailable = null,
        [FromQuery] DateTimeOffset? asOf = null)
    {
        var sessions = await service.GetUpcomingAsync(course, minSeatsAvailable, asOf, cancellationToken);
        return sessions.Select(session => session.ToDto());
    }

    [HttpGet("summary")]
    public Task<IEnumerable<StudySessionSummary>> GetSummaries(
        CancellationToken cancellationToken,
        [FromQuery] int? minSeatsAvailable = null) =>
        service.GetSummariesAsync(minSeatsAvailable, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudySessionDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var session = await service.GetByIdAsync(id, cancellationToken);
        return session is null ? NotFound() : session.ToDto();
    }

    [HttpPost]
    public async Task<ActionResult<StudySessionDto>> Create(CreateStudySessionDto session, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(session.ToEntity(), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created.ToDto());
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateStudySessionDto session, CancellationToken cancellationToken) =>
        await service.UpdateAsync(id, session.ToEntity(), cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPut("{id:int}/rating")]
    public async Task<IActionResult> RateSession(int id, RatingSubmission submission, CancellationToken cancellationToken) =>
        await service.RateAsync(id, submission.Value, cancellationToken) ? NoContent() : NotFound();
}
