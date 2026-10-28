# Walkthrough 06: SOLID in Practice - a Service, a Repository, and Dependency Injection

This is the step-by-step walkthrough behind [Lecture 06](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture06): taking a controller that does everything and splitting it up, so you can see **Single Responsibility** and **Dependency Inversion** (and, as a side effect, **Liskov Substitution**) applied to real code instead of shapes and printers. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-04.md](WALKTHROUGH-04.md) - a controller with full CRUD, filtering, sorting and ratings over an in-memory list. (Lecture 05 was the Deadline 1 review and has no walkthrough of its own.) Apply these same steps to **your own team's project**, not just this repository. This is a refactor: no endpoint changes behavior, so your `.http` requests should return exactly what they returned before.

The refactor happens in **two stages, and the project builds and behaves the same at the end of each one**:

1. **Stage 1 - Single Responsibility (steps 1-5):** move the rules out of the controller into a service. The service still keeps the data (the static list) for now.
2. **Stage 2 - Dependency Inversion + Dependency Injection (steps 6-10):** move the data out of the service behind a repository interface, and let the container wire everything together.

Then step 11 shows why every implementation of that interface has to honor the same contract (Liskov).

## 0. What's Wrong With the Controller Today

Open `Controllers/StudySessionsController.cs`. A single class currently:

- owns the data (a `static List<StudySession>` plus its sample data),
- decides what counts as "upcoming", how sessions are filtered and ordered,
- applies the rating rule,
- *and* translates HTTP requests into all of that.

Ask yourself three questions: what changes if we move to a database? If we add a new rule? If we want to test the rule without starting a web server? Every answer is "edit the controller" - that's a class with too many reasons to change.

# Stage 1 - Single Responsibility: a Service

## 1. Define the Service Interface

Add `Services/IStudySessionService.cs` - what the controller needs from the app's rules, without knowing which class provides them:

```csharp
using CoolApp.Models;

namespace CoolApp.Services;

// What the controller needs from the app's rules, without knowing which class provides
// them (Dependency Inversion again, one layer up): the controller depends on this
// interface, so a test can hand it a fake service and check what the controller does
// with the results - status codes, 404s - without any real data or rules involved.
//
// "Not found" is reported as null/false rather than an exception, so the controller can
// turn it into a 404 without a try/catch.
public interface IStudySessionService
{
    IEnumerable<StudySession> GetUpcoming(string? course, int? minSeatsAvailable, DateTimeOffset? asOf);

    IEnumerable<StudySessionSummary> GetSummaries(int? minSeatsAvailable);

    StudySession? GetById(int id);

    StudySession Create(StudySession session);

    bool Update(int id, StudySession session);

    bool Delete(int id);

    bool Rate(int id, int value);
}
```

## 2. Add the Service (the Rules, and for Now the Data)

Add `Services/StudySessionService.cs`. The rules move here from the controller: what "upcoming" means, filtering, sorting, applying a rating. The static list and its sample data move here too - **on purpose**: this stage only separates the rules from the HTTP handling. The class still has two jobs (rules *and* storage); stage 2 fixes that.

```csharp
using CoolApp.Extensions;
using CoolApp.Models;

namespace CoolApp.Services;

// The rules of the app (what counts as "upcoming", how sessions are filtered and
// ordered, how a rating is applied) live here - not in the controller, which only
// translates HTTP to and from these calls (Single Responsibility).
//
// For now the data still lives here too, in a static list (the controller used to own
// it). The next step moves it out behind an interface, so this class stops knowing
// where sessions are stored.
public class StudySessionService : IStudySessionService
{
    // In-memory sample data. A real implementation would use a database (see Lab Assignment #2).
    // Static, because the service is scoped (a new instance per request) and the data has to survive.
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

    public IEnumerable<StudySession> GetUpcoming(string? course, int? minSeatsAvailable, DateTimeOffset? asOf)
    {
        // asOf: a real (not just documented) use of UpcomingOnly's optional argument -
        // omit it for "upcoming as of right now" (every other call site does this), or
        // pass it to preview what the list will look like at a future moment.
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

    public IEnumerable<StudySessionSummary> GetSummaries(int? minSeatsAvailable) =>
        // This endpoint deliberately never exposes a course filter - only minSeatsAvailable.
        // Filter(minSeatsAvailable: minSeatsAvailable) skips `course` (Filter's first
        // parameter) entirely: there is no positional way to write that call. Naming
        // isn't optional polish here, it's the only way to reach the parameter you want
        // without also having to know and restate the one you don't.
        Sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
            .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));

    public StudySession? GetById(int id) => Sessions.FirstOrDefault(s => s.Id == id);

    public StudySession Create(StudySession session)
    {
        session.Id = Sessions.Count == 0 ? 1 : Sessions.Max(s => s.Id) + 1;
        Sessions.Add(session);
        return session;
    }

    public bool Update(int id, StudySession session)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return false;
        }

        existing.Course = session.Course;
        existing.Topic = session.Topic;
        existing.Location = session.Location;
        existing.StartsAt = session.StartsAt;
        existing.HostName = session.HostName;
        existing.SeatsAvailable = session.SeatsAvailable;

        return true;
    }

    public bool Delete(int id)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return false;
        }

        Sessions.Remove(existing);
        return true;
    }

    public bool Rate(int id, int value)
    {
        var existing = Sessions.FirstOrDefault(s => s.Id == id);
        if (existing is null)
        {
            return false;
        }

        // [Range] on RatingSubmission.Value already rejected anything outside 1-5
        // before this line runs - the Rating constructor re-checks anyway, since a
        // Rating that's out of range has to be impossible everywhere, not just here.
        existing.HostRating = new Rating(value);
        return true;
    }
}
```

The list is `static` because the service is registered as scoped (a new instance per request), and the data has to survive between requests.

## 3. Slim Down the Controller

The controller now only translates HTTP into service calls and results into status codes. Replace `Controllers/StudySessionsController.cs`:

```csharp
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
```

It takes `IStudySessionService`, not the concrete class, so it depends on an abstraction too.

## 4. Register the Service (Dependency Injection)

In `Program.cs`, add a `using` at the very top and register the service right after `AddSwaggerGen()`:

```csharp
using CoolApp.Services;

// ...

builder.Services.AddScoped<IStudySessionService, StudySessionService>();
```

When a constructor asks for an `IStudySessionService`, ASP.NET Core now hands out a `StudySessionService` (scoped = one instance per request). The controller never writes `new` for its dependency.

## 5. Checkpoint: Build and Try It

```pwsh
dotnet build
dotnet run
```

Run the same `CoolApp.http` requests as before - nothing should behave differently (the full list is in step 10). The code works at this point; the only thing left to improve is that `StudySessionService` still knows where sessions are stored.

# Stage 2 - Dependency Inversion: a Repository

## 6. Define the Repository Interface

Add `Repositories/IStudySessionRepository.cs`:

```csharp
using CoolApp.Models;

namespace CoolApp.Repositories;

// The abstraction the rest of the app depends on (Dependency Inversion): callers say
// *what* they need stored and retrieved, never *where* it lives. Today the only
// implementation keeps sessions in memory; Beta swaps in Entity Framework without
// touching anything that uses this interface.
//
// The contract is part of the interface, not just the signatures - every implementation
// has to honor it, or callers can't safely substitute one for another (Liskov):
//   - GetById returns null for a missing id; it never throws.
//   - GetById may hand back the stored instance *or a copy*. Callers must not rely on
//     mutating what they got back - the only way to persist a change is Update.
//   - Update and Delete return false (and change nothing) when the id doesn't exist.
public interface IStudySessionRepository
{
    IReadOnlyList<StudySession> GetAll();

    StudySession? GetById(int id);

    // Assigns the new session's Id and returns the stored session.
    StudySession Add(StudySession session);

    bool Update(StudySession session);

    bool Delete(int id);
}
```

The interface says *what* is stored and retrieved, never *where*. Note that the comment block on top is part of the design: it's the contract every implementation has to honor (step 11 shows why that matters).

## 7. Move the Data Into an In-Memory Implementation

Add `Repositories/InMemoryStudySessionRepository.cs` - the list and its sample data move here from the service, no longer `static`:

```csharp
using CoolApp.Models;

namespace CoolApp.Repositories;

// In-memory sample data. A real implementation would use a database (see Lab Assignment #2).
//
// Registered as a singleton in Program.cs: the data lives in this instance's list, so a
// new instance per request would forget every session between requests.
public class InMemoryStudySessionRepository : IStudySessionRepository
{
    private readonly List<StudySession> _sessions = new()
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

    public IReadOnlyList<StudySession> GetAll() => _sessions.ToList();

    // Returns the stored instance itself. That's an implementation detail, not part of
    // the contract (see IStudySessionRepository): callers still have to call Update.
    public StudySession? GetById(int id) => _sessions.FirstOrDefault(s => s.Id == id);

    public StudySession Add(StudySession session)
    {
        session.Id = _sessions.Count == 0 ? 1 : _sessions.Max(s => s.Id) + 1;
        _sessions.Add(session);
        return session;
    }

    public bool Update(StudySession session)
    {
        var index = _sessions.FindIndex(s => s.Id == session.Id);
        if (index < 0)
        {
            return false;
        }

        _sessions[index] = session;
        return true;
    }

    public bool Delete(int id)
    {
        var existing = GetById(id);
        return existing is not null && _sessions.Remove(existing);
    }
}
```

## 8. Make the Service Depend on the Repository

Replace `Services/StudySessionService.cs`. Compare it with the stage 1 version: the static list and sample data are gone, the class now receives the repository through its constructor (a primary constructor, C# 12), every list access became a repository call, and `Update`/`Rate` end with an explicit `repository.Update(existing)` (step 11 explains why):

```csharp
using CoolApp.Extensions;
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Services;

// The rules of the app (what counts as "upcoming", how sessions are filtered and
// ordered, how a rating is applied) live here - not in the controller, which only
// translates HTTP to and from these calls (Single Responsibility), and not in the
// repository, which only stores and retrieves.
//
// The repository arrives through the constructor (Dependency Injection) instead of
// being created here, so this class doesn't know or care where sessions are stored.
public class StudySessionService(IStudySessionRepository repository) : IStudySessionService
{
    public IEnumerable<StudySession> GetUpcoming(string? course, int? minSeatsAvailable, DateTimeOffset? asOf)
    {
        // asOf: a real (not just documented) use of UpcomingOnly's optional argument -
        // omit it for "upcoming as of right now" (every other call site does this), or
        // pass it to preview what the list will look like at a future moment.
        //
        // course/minSeatsAvailable are supplied in Filter's own declared order here, so
        // naming them is a readability choice, not a requirement - contrast GetSummaries
        // below, which skips the first one entirely and *must* name the second.
        //
        // LINQ (Where, inside UpcomingOnly/Filter) filters; IComparable<StudySession>
        // (via CompareTo) lets List<T>.Sort() put what's left in start-time order with
        // no comparer.
        var upcoming = repository.GetAll()
            .UpcomingOnly(asOf: asOf)
            .Filter(course, minSeatsAvailable)
            .ToList();
        upcoming.Sort();
        return upcoming;
    }

    public IEnumerable<StudySessionSummary> GetSummaries(int? minSeatsAvailable) =>
        // This endpoint deliberately never exposes a course filter - only minSeatsAvailable.
        // Filter(minSeatsAvailable: minSeatsAvailable) skips `course` (Filter's first
        // parameter) entirely: there is no positional way to write that call. Naming
        // isn't optional polish here, it's the only way to reach the parameter you want
        // without also having to know and restate the one you don't.
        repository.GetAll().UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
            .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));

    public StudySession? GetById(int id) => repository.GetById(id);

    public StudySession Create(StudySession session) => repository.Add(session);

    public bool Update(int id, StudySession session)
    {
        var existing = repository.GetById(id);
        if (existing is null)
        {
            return false;
        }

        existing.Course = session.Course;
        existing.Topic = session.Topic;
        existing.Location = session.Location;
        existing.StartsAt = session.StartsAt;
        existing.HostName = session.HostName;
        existing.SeatsAvailable = session.SeatsAvailable;

        // Without this line the in-memory repository would still appear to work, because
        // GetById happens to return the stored instance. A repository that returns copies
        // (like a database would) would silently lose the change - see the contract on
        // IStudySessionRepository.
        return repository.Update(existing);
    }

    public bool Delete(int id) => repository.Delete(id);

    public bool Rate(int id, int value)
    {
        var existing = repository.GetById(id);
        if (existing is null)
        {
            return false;
        }

        // [Range] on RatingSubmission.Value already rejected anything outside 1-5
        // before this line runs - the Rating constructor re-checks anyway, since a
        // Rating that's out of range has to be impossible everywhere, not just here.
        existing.HostRating = new Rating(value);
        return repository.Update(existing);
    }
}
```

## 9. Register the Repository

In `Program.cs`, add one more `using` (so both are there), and register the repository just above the service registration:

```csharp
using CoolApp.Repositories;
using CoolApp.Services;

// ...

builder.Services.AddSingleton<IStudySessionRepository, InMemoryStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();
```

ASP.NET Core now builds the whole chain for you: a request for `StudySessionsController` needs an `IStudySessionService`, which needs an `IStudySessionRepository`. Neither class ever writes `new` for its dependency.

The lifetimes are deliberate: the repository is a **singleton** because the in-memory data lives inside the instance (a fresh instance per request would forget everything). The service is **scoped** (one per request). A service may only depend on services with the same or a longer lifetime - scoped depending on singleton is fine; the reverse is the "captive dependency" trap from this week's DI theory. When you move to Entity Framework in Beta, the repository will become scoped (because `DbContext` is), and the service stays as it is.

## 10. Checkpoint: Build and Try It Again

Restart the app and run the same `CoolApp.http` requests as before - nothing should behave differently:

- `GET /api/studysessions`, `GET /api/studysessions/1`, `GET /api/studysessions/999` (expect 404), `GET /api/studysessions/summary`.
- `POST` a valid session (201), then an invalid one (400 - validation still happens before the controller runs).
- `PUT` the new session and `GET` it back to confirm the change stuck; `PUT` a missing id (404).
- `PUT /api/studysessions/{id}/rating` with `4` (204), with `9` (400), on a missing id (404).
- `DELETE` it (204), then again (404).
- Filters: `?course=...`, `?minSeatsAvailable=...`, `?asOf=...`.

## 11. The Liskov Gotcha: a Repository That Returns Copies

Today's in-memory repository returns the *stored object itself* from `GetById`. That is why this would appear to work even **without** the explicit `repository.Update(existing)` at the end of `Update` and `Rate` - changing the returned object changes the stored one. A database-backed repository cannot do that: it returns a copy, and changes only persist when you save them.

You can prove it. Temporarily add `Repositories/CopyingStudySessionRepository.cs` (an implementation of the same interface that hands out copies):

```csharp
using CoolApp.Models;

namespace CoolApp.Repositories;

// Same contract as InMemoryStudySessionRepository, but - like a database would -
// GetById/GetAll hand back copies. Mutating one changes nothing until Update is called.
public class CopyingStudySessionRepository : IStudySessionRepository
{
    private readonly InMemoryStudySessionRepository _inner = new();

    private static StudySession Copy(StudySession s) => new()
    {
        Id = s.Id, Course = s.Course, Topic = s.Topic, Location = s.Location,
        StartsAt = s.StartsAt, HostName = s.HostName, SeatsAvailable = s.SeatsAvailable,
        HostRating = s.HostRating
    };

    public IReadOnlyList<StudySession> GetAll() => _inner.GetAll().Select(Copy).ToList();
    public StudySession? GetById(int id) => _inner.GetById(id) is { } s ? Copy(s) : null;
    public StudySession Add(StudySession session) => _inner.Add(session);
    public bool Update(StudySession session) => _inner.Update(Copy(session));
    public bool Delete(int id) => _inner.Delete(id);
}
```

Register it instead of the in-memory one in `Program.cs`, run the app, and `PUT` an update and a rating - both work, because the service calls `repository.Update`. Now delete the `return repository.Update(existing);` line (and the one in `Rate`) and try again: the in-memory repository still looks fine, but with the copying repository every update is silently lost. That is a Liskov violation in miniature: two implementations of the same interface that are *not* interchangeable, because the contract ("changes persist only through `Update`") was only honored by accident. Writing the contract down in the interface's comment, and making the service obey it, is the fix. Remove the temporary repository and registration when you're done.

## What About the Other Principles?

- **Open/Closed**: `Filter` already applies each optional filter independently, so adding another filter means adding a block, not rewriting the existing ones.
- **Interface Segregation**: `UpcomingOnly` and `Filter` take `IEnumerable<StudySession>`, not `List<StudySession>` - they depend on the narrowest thing they need, so they can't `Add` or `Remove` by accident and work on any collection.
- **Over-engineering warning**: this was a lot of structure for a small app. Weigh it for your own project - the point is the reasons to change, not the number of files.

## Next Steps

Once this is done, update `ROADMAP.md` (the Beta item "Dependency Injection used for the repository/service layer" can be checked off) and `README.md`'s "Project Map" and "Current Examples". Next: [WALKTHROUGH-08.md](WALKTHROUGH-08.md) adds automated tests and then makes these layers async. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list.
