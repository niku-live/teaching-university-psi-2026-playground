# Walkthrough 10: Code Quality Checked by the Build, then DTOs, a Generic Cache and Shared Memory

This is the step-by-step walkthrough behind [Lecture 10](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture10): letting the **build** check the code against a coding standard (the .NET code analyzers and `.editorconfig`), then separating the API contract from the database entity with **DTOs**, and adding a **generic** in-memory cache on a **`ConcurrentDictionary`**, which every request shares. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-09.md](WALKTHROUGH-09.md): the API in `CoolApp.Api/` with Entity Framework Core behind `IStudySessionRepository`, a service layer, and an xUnit test project. Apply these same steps to **your own team's project**, not just this repository. Stage 1 follows the [Software Construction theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/09-software-construction.qmd) (standards enforced by the build, complexity metrics, mutation testing); stage 2 the [Generics theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/09-generics.qmd). Stage 2 covers the last open Lab Assignment #2 requirements: "create and use at least 1 generic type" and "a shared memory scenario handled with concurrent collections or locks", plus request and response DTOs.

The work happens in **two stages, and the project builds and all tests pass at the end of each one**:

1. **Stage 1 - Code quality checked by the build (steps 1-5):** a `Directory.Build.props` that turns on the recommended analyzers and the `.editorconfig` style rules for every project, fixing or deliberately excluding what they find, and then making any new warning fail the build.
2. **Stage 2 - DTOs, a generic cache and shared memory (steps 6-14):** `StudySessionDto`/`CreateStudySessionDto`/`UpdateStudySessionDto` at the API's edge, then a generic `ICache<TKey, TValue>` with a `ConcurrentDictionary` implementation, used by a caching decorator around the service.

Step 15 (mutation testing with Stryker.NET) is optional.

# Stage 1 - Code Quality Checked by the Build

## 1. Turn On the Analyzers for Every Project

The .NET SDK ships with code analyzers: rules named `CAxxxx` (code quality: design, performance, security, reliability) and `IDExxxx` (code style). Most of them are off or only suggestions by default. Create `Directory.Build.props` in the repository root, next to `CoolApp.sln`. MSBuild picks it up for every project in that folder and below, so the API and all test projects get the same standard without editing each `.csproj`:

```xml
<Project>

  <!-- Applies to every .NET project below this folder (the API and all three test projects):
       the coding standard, checked by the build instead of only by whoever reviews the PR. -->
  <PropertyGroup>
    <!-- The recommended set of .NET code quality rules (CAxxxx) for the current SDK -->
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <!-- The code style rules in .editorconfig (IDExxxx) are checked by the build too, not only in the IDE -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>

</Project>
```

- `AnalysisLevel` `latest-recommended` turns on the recommended set of rules as warnings.
- `EnforceCodeStyleInBuild` makes `dotnet build` check the `IDE` style rules too, not only your editor. Without it, the rules only show up for whoever happens to use an IDE that reads them.

## 2. Build, and Read What It Finds

```pwsh
dotnet build
```

On StudySpot the build still succeeds, with 33 warnings of three kinds:

- `CA1707` "Remove the underscores from member name" - in every test, because the tests are named `Method_Scenario_Result` (Lecture 08).
- `CA1036` "StudySession should define operators `==`, `!=`, `<`, `<=`, `>`, `>=` and Equals since it implements IComparable".
- `CA1816` "Change StudySpotApiFactory.DisposeAsync() to call GC.SuppressFinalize(object)".

Every warning gets one of two answers: **fix the code**, or **decide the rule doesn't apply here and write down why**. Both are fine; leaving it unanswered is not.

## 3. Add an `.editorconfig` With the Style Rules and the Exceptions

Create `.editorconfig` in the repository root. It sets style rules (checked by the build because of step 1), turns on the complexity rules, and records each decision not to follow a rule, next to the files it applies to:

```ini
root = true

[*.cs]
# Code style, checked by the build (EnforceCodeStyleInBuild in Directory.Build.props)
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning
dotnet_diagnostic.IDE0161.severity = warning
dotnet_diagnostic.IDE0011.severity = warning

# Complexity rules are off by default: cyclomatic complexity (CA1502) and class coupling (CA1506)
dotnet_diagnostic.CA1502.severity = warning
dotnet_diagnostic.CA1506.severity = warning

# Test projects: names follow Method_Scenario_Result (Lecture 08), so underscores are allowed, and
# an expected array written inline in an assertion is clearer than a static field (CA1861)
[CoolApp.Api.Tests.*/**.cs]
dotnet_diagnostic.CA1707.severity = none
dotnet_diagnostic.CA1861.severity = none

# Migrations are generated by "dotnet ef migrations add": never edited by hand, so never checked
[CoolApp.Api/Migrations/**.cs]
generated_code = true

# Exceptions are decisions too - each one says why.
#
# Program.cs is the composition root: wiring every service together is its job, so it is coupled
# to everything on purpose.
[CoolApp.Api/Program.cs]
dotnet_diagnostic.CA1506.severity = none

# StudySession implements IComparable<StudySession> only so List<T>.Sort() orders sessions by start
# time. CA1036 asks for ==, <, > and Equals to match, but two sessions starting at the same time are
# not "equal" - an entity is equal to another one only if they have the same Id.
[CoolApp.Api/Models/StudySession.cs]
dotnet_diagnostic.CA1036.severity = none
```

- `csharp_style_namespace_declarations` and `csharp_prefer_braces` are style rules your team agrees on once, so reviews don't argue about them. The `IDE0161`/`IDE0011` severity lines make them build warnings.
- `CA1502` (cyclomatic complexity) and `CA1506` (class coupling) report methods and types that are getting hard to follow. The theory lecture explains what the numbers mean.
- **Exceptions are decisions too**: the test projects allow underscores; migrations are `generated_code`, so no rule checks code nobody writes by hand; `Program.cs` is coupled to everything because wiring things together is its job; and `StudySession` sorts by start time, but two sessions starting at the same time aren't "equal", so `CA1036`'s `==` would be wrong.

## 4. Fix What Should Be Fixed

`CA1816` is a real fix. In `CoolApp.Api.Tests.XUnit/Integration/StudySpotApiFactory.cs`, add one line at the end of `DisposeAsync`:

```csharp
    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // SQLite keeps pooled connections open, which keeps the file locked on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
        GC.SuppressFinalize(this);
    }
```

## 5. Make Warnings Fail the Build, and Check the Dependencies

Now that there are no warnings, keep it that way. Add `TreatWarningsAsErrors` and `WarningsNotAsErrors` inside the `PropertyGroup` of `Directory.Build.props`; the whole file is now:

```xml
<Project>

  <!-- Applies to every .NET project below this folder (the API and all three test projects):
       the coding standard, checked by the build instead of only by whoever reviews the PR. -->
  <PropertyGroup>
    <!-- The recommended set of .NET code quality rules (CAxxxx) for the current SDK -->
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <!-- The code style rules in .editorconfig (IDExxxx) are checked by the build too, not only in the IDE -->
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <!-- Still reported, but they don't fail the build:
         CA1502/CA1506 - a complexity number over the limit is a reason to look at the code, not proof it is wrong;
         NU1901-NU1904 - a vulnerability published tomorrow in a package we use would otherwise break
         every build overnight. "dotnet list package" with the vulnerable option lists them. -->
    <WarningsNotAsErrors>CA1502;CA1506;NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
  </PropertyGroup>

</Project>
```

- `TreatWarningsAsErrors` fails the build on any warning, so a new one can't be merged "for now". Turning it on only once the code is clean is how a standard is adopted in existing code (theory lecture, "Adopting standards in existing code"): with 33 warnings, the build would have stopped at the first project with an error.
- `WarningsNotAsErrors` keeps a few as warnings, each for a reason: complexity numbers are a reason to look, not proof something is wrong, and a vulnerability published tomorrow in a package you use shouldn't break every build overnight (the `NU190x` warnings come from NuGet's audit).

```pwsh
dotnet build
dotnet test
```

The build has no warnings, and the 30 tests from Lecture 09 still pass. From now on, a pull request that adds a warning fails the build, in CI too once there is a pipeline (Lab Assignment #3).

The theory lecture also measures **dependencies**. These need no setup:

```pwsh
dotnet list package --vulnerable --include-transitive
dotnet list package --outdated
```

# Stage 2 - DTOs, a Generic Cache and Shared Memory

## 6. Why DTOs: the Entity Is Not the Contract

Until now the controller bound requests to the `StudySession` **entity** and returned it. That makes the database table the API's contract: every property you add to the entity (a navigation property, something only the server needs) appears in the JSON, and every property a client sends is bound, even ones it shouldn't choose. That second problem is **over-posting**. Try it with this request, before the next steps (it's in `CoolApp.Api/CoolApp.http`):

```http
### Over-posting: try to choose the version yourself (since Lecture 10 the "version" in the response is 0)
# Before the DTOs the API bound the StudySession entity, and stored "version": 50 as sent.
POST {{HostAddress}}/api/studysessions
Content-Type: application/json

{
  "version": 50,
  "course": "Software Development I",
  "topic": "Over-posting",
  "location": "MIF, room 401",
  "startsAt": "2027-09-23T18:00:00Z",
  "hostName": "You",
  "seatsAvailable": 4
}
```

The response has `"version": 50`. A **DTO** (data transfer object) is a type made only for the contract: one for what the API sends back, and one for each request, with only the fields a client may choose.

## 7. Add the DTOs

Create a `CoolApp.Api/Dtos/` folder. The response is a positional `record`: immutable, with value equality (two DTOs with the same values are equal, which the tests use):

```csharp
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
```

The create request has only what a client chooses, and the validation moves here from the entity (`IValidatableObject` from Lecture 03):

```csharp
using System.ComponentModel.DataAnnotations;

namespace CoolApp.Dtos;

// What a client sends to create a study session: the request contract. Only the fields a
// client is allowed to choose - no Id, Version or HostRating. Binding the entity itself let a
// client "over-post" those: a POST with "version": 50 was stored as sent. (The Id was only safe
// because EfStudySessionRepository.AddAsync happens to reset it; every new entity property
// would have needed a guard like that.)
//
// Validation lives here, at the trust boundary (theory 09: invalid input from outside is
// expected, so it's rejected with a 400 the client can act on).
public record CreateStudySessionDto : IValidatableObject
{
    [Required, StringLength(100)]
    public string Course { get; init; } = string.Empty;

    [Required, StringLength(200)]
    public string Topic { get; init; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; init; } = string.Empty;

    public DateTimeOffset StartsAt { get; init; }

    [Required, StringLength(100)]
    public string HostName { get; init; } = string.Empty;

    // 0 is a valid, if unusual, input: a host can list a session as already full.
    [Range(0, 100)]
    public int SeatsAvailable { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt <= DateTimeOffset.UtcNow)
        {
            yield return new ValidationResult("Starts at must be in the future.", [nameof(StartsAt)]);
        }
    }
}
```

An update carries the same fields plus the `Version` the client read (Lecture 09):

```csharp
namespace CoolApp.Dtos;

// An update carries everything a create does, plus the Version the client read (GET), so
// the repository can tell whether someone else saved the session in between (Lecture 09's 409).
public record UpdateStudySessionDto : CreateStudySessionDto
{
    public int Version { get; init; }
}
```

In `CoolApp.Api/Models/StudySession.cs`, remove `IValidatableObject` and its `Validate` method (it's in the DTO now), and the `[BindNever]` on `HostRating` with its `using Microsoft.AspNetCore.Mvc.ModelBinding;` (nothing binds the entity any more). Keep the `[Required]`/`[StringLength]` attributes: Entity Framework uses them for the columns, and removing them would change the model and need a new migration. `StudySessionSummary` (Lecture 04) already was a small response DTO, so it stays as it is.

## 8. Map Between Entities and DTOs

One static class of extension methods does the mapping in both directions:

```csharp
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
```

## 9. Use the DTOs in the Controller

The controller is the edge of the API, so the mapping happens there. The service and the repository keep working with the entity. In `CoolApp.Api/Controllers/StudySessionsController.cs`, add `using CoolApp.Dtos;` and change these actions:

```csharp
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
```

`Status` is an enum sent as its name. Put the converter on the enum itself (`CoolApp.Api/Models/SessionStatus.cs`), so a test's `HttpClient` reading a `StudySessionDto` understands `"Scheduled"` as well:

```csharp
using System.Text.Json.Serialization;

namespace CoolApp.Models;

// Sent and read as its name ("Scheduled"), not a number - wherever it is serialized, including
// a test's HttpClient reading a StudySessionDto, not only by the app's own JSON options.
[JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))]
public enum SessionStatus
{
    Scheduled,
    Full,
}
```

## 10. Update the Tests, and Test the Over-Posting

The compiler shows what needs changing. In `Unit/StudySessionsControllerTests.cs`, add `using CoolApp.Dtos;`, a helper that builds a request, and compare DTOs by value:

```csharp
    private static CreateStudySessionDto NewSession() => new()
    {
        Course = "Software Development I",
        Topic = "Controller tests",
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };
```

- `GetById_ServiceFindsTheSession_ReturnsIt`: `Assert.Equal(session.ToDto(), result.Value);` instead of `Assert.Same(...)`.
- `Create_ReturnsCreatedAtGetByIdWithTheNewId`: `_controller.Create(NewSession(), Cancellation)` and `Assert.Equal(created.ToDto(), createdAt.Value);`.

In `Integration/StudySessionsApiTests.cs`, replace `using CoolApp.Models;` with `using CoolApp.Dtos;` and read `StudySessionDto` instead of `StudySession`. A DTO is immutable, so the update tests change a copy with `with`:

```csharp
        var response = await _client.PutAsJsonAsync(created.Headers.Location, session! with { SeatsAvailable = 2 }, Cancellation);
```

and add a test for step 6's request:

```csharp
    // Over-posting: the request contract (CreateStudySessionDto) has no id or version, so a
    // client that sends them anyway can't choose them. Before the DTOs, this session was
    // stored with version 50.
    [Fact]
    public async Task Create_WithIdAndVersion_IgnoresThem()
    {
        var session = new
        {
            id = 999,
            version = 50,
            course = "Software Development I",
            topic = "Over-posting",
            location = "MIF, room 401",
            startsAt = DateTimeOffset.UtcNow.AddDays(7),
            hostName = "Ieva",
            seatsAvailable = 4
        };

        var response = await _client.PostAsJsonAsync("/api/studysessions", session, Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<StudySessionDto>(Cancellation);
        Assert.Equal(0, created!.Version);
    }
```

Run `dotnet test`: 31 tests pass. The JSON the React app gets hasn't changed (same fields, same names), so the frontend needs nothing.

## 11. A Generic Cache Interface

Lab Assignment #2 asks for a **shared memory scenario**. Since Lecture 09 the sessions live in the database, so there is no shared `List` any more. A typical new one is a **cache**: reading a session by id from memory instead of the database, shared by all requests. Lab Assignment #2 also asks for a **generic type**, and a cache is a natural one: the same code works for any key and value. Create `CoolApp.Api/Caching/ICache.cs`:

```csharp
namespace CoolApp.Caching;

// A generic cache: one implementation works for any key and value type, checked by the
// compiler - ICache<int, StudySession> only accepts int keys and only hands back sessions,
// with no casts from object (what a non-generic cache would need).
//
// The constraint `where TKey : notnull` says what the implementation needs from the key
// type: a dictionary can't store a null key.
//
// TKey and TValue are invariant (no `in`/`out`, theory 09): TValue is both returned
// (GetOrAddAsync) and passed in (as the result of `load`), so neither variance would be safe.
public interface ICache<TKey, TValue>
    where TKey : notnull
{
    // Returns the cached value for the key, or calls `load`, caches what it returns
    // (unless it's null - "not found" isn't cached) and returns that.
    Task<TValue> GetOrAddAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> load, CancellationToken cancellationToken);

    // Forgets the key, so the next GetOrAddAsync loads it again.
    void Remove(TKey key);
}
```

## 12. Implement It With a `ConcurrentDictionary`

`CoolApp.Api/Caching/InMemoryCache.cs`:

```csharp
using System.Collections.Concurrent;

namespace CoolApp.Caching;

// Shared memory: the app registers this as a singleton (Program.cs), so ONE instance is used
// by every request, and ASP.NET Core runs requests in parallel on different threads.
//
// A plain Dictionary<TKey, TValue> isn't safe for that: two threads writing at the same time
// can corrupt it (lost entries, an exception, or an endless loop inside it). The usual fixes
// are a `lock` around every access, or a concurrent collection - ConcurrentDictionary does
// the locking itself, per part of the dictionary, so readers don't wait for each other.
public sealed class InMemoryCache<TKey, TValue> : ICache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, TValue> _items = new();

    public async Task<TValue> GetOrAddAsync(TKey key, Func<TKey, CancellationToken, Task<TValue>> load, CancellationToken cancellationToken)
    {
        if (_items.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = await load(key, cancellationToken);
        if (loaded is null)
        {
            return loaded;
        }

        // Two requests can miss the same key at the same moment and both load it. GetOrAdd
        // is atomic: the first value stored wins, and both requests get that same value back.
        // (Each check-then-act step on its own would be a race: "if not there, add" can be
        // interrupted between the check and the add.)
        return _items.GetOrAdd(key, loaded);
    }

    public void Remove(TKey key) => _items.TryRemove(key, out _);
}
```

If you'd rather use a `lock`, it looks like this, and every access to the dictionary has to be inside one:

```csharp
private readonly Dictionary<TKey, TValue> _items = new();
private readonly Lock _lock = new();

lock (_lock)
{
    if (!_items.TryGetValue(key, out var existing)) { _items[key] = loaded; existing = loaded; }
    return existing;
}
```

(An `await` isn't allowed inside a `lock`, which is why the loading happens outside it in both versions.)

## 13. Use It From a Caching Decorator

The service shouldn't know about caching (Single Responsibility), and the controller shouldn't either. A **decorator** implements `IStudySessionService` by wrapping the real one. Create `CoolApp.Api/Services/CachedStudySessionService.cs`:

```csharp
using CoolApp.Caching;
using CoolApp.Models;

namespace CoolApp.Services;

// A decorator: implements IStudySessionService by wrapping another IStudySessionService and
// adding one thing - caching a session by id. The controller still asks for
// IStudySessionService and doesn't know the cache exists (Open/Closed: the behavior is
// extended without editing StudySessionService).
//
// Every change goes to the real service first and then removes the session from the cache,
// so the next read loads the new version. (This cache lives in one app's memory: with
// several instances of the app, each would need to be told - a distributed cache like
// Redis solves that.)
public class CachedStudySessionService(StudySessionService inner, ICache<int, StudySession?> cache) : IStudySessionService
{
    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        cache.GetOrAddAsync(id, inner.GetByIdAsync, cancellationToken);

    public async Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.UpdateAsync(id, session, cancellationToken);
        }
        finally
        {
            // Also after a 409 conflict: someone else changed the session, so the cached copy is old.
            cache.Remove(id);
        }
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var deleted = await inner.DeleteAsync(id, cancellationToken);
        cache.Remove(id);
        return deleted;
    }

    public async Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken)
    {
        var rated = await inner.RateAsync(id, value, cancellationToken);
        cache.Remove(id);
        return rated;
    }

    // Lists and new sessions aren't cached: passed straight through.
    public Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken) =>
        inner.GetUpcomingAsync(course, minSeatsAvailable, asOf, cancellationToken);

    public Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken) =>
        inner.GetSummariesAsync(minSeatsAvailable, cancellationToken);

    public Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken) =>
        inner.CreateAsync(session, cancellationToken);
}
```

Register it in `CoolApp.Api/Program.cs` (add `using CoolApp.Caching;`), replacing the `AddScoped<IStudySessionService, StudySessionService>()` line:

```csharp
// The generic cache, registered once for every type argument (an "open generic"): whoever
// asks for ICache<int, StudySession?> - or any other ICache<TKey, TValue> - gets an
// InMemoryCache<int, StudySession?>. A singleton, so every request shares the same one.
builder.Services.AddSingleton(typeof(ICache<,>), typeof(InMemoryCache<,>));

// The controller asks for IStudySessionService and gets the caching decorator, which wraps
// the real StudySessionService (registered as itself, so the decorator can ask for it).
builder.Services.AddScoped<StudySessionService>();
builder.Services.AddScoped<IStudySessionService, CachedStudySessionService>();
```

- `typeof(ICache<,>)` is an **open generic** registration: one line for every `ICache<TKey, TValue>` anyone asks for.
- **Singleton**, so all requests share one cache. That's exactly what makes it shared memory, and why it needs a thread-safe collection. (The decorator itself is scoped, like the service and the `DbContext` it uses.)

## 14. Test the Cache, Including Many Threads at Once

`CoolApp.Api.Tests.XUnit/Unit/InMemoryCacheTests.cs`: the generic class with two different type arguments, and 100 parallel requests for the same key:

```csharp
using CoolApp.Caching;

namespace CoolApp.Api.Tests.XUnit.Unit;

// The generic cache with two different type arguments - the same code, checked by the
// compiler for each: InMemoryCache<string, string> and InMemoryCache<int, object>.
public class InMemoryCacheTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetOrAdd_SameKeyTwice_LoadsOnce()
    {
        var cache = new InMemoryCache<string, string>();
        var loads = 0;
        Task<string> Load(string key, CancellationToken _)
        {
            loads++;
            return Task.FromResult(key.ToUpperInvariant());
        }

        await cache.GetOrAddAsync("psi", Load, Cancellation);
        var second = await cache.GetOrAddAsync("psi", Load, Cancellation);

        Assert.Equal("PSI", second);
        Assert.Equal(1, loads);
    }

    [Fact]
    public async Task Remove_NextGetOrAdd_LoadsAgain()
    {
        var cache = new InMemoryCache<string, string>();
        await cache.GetOrAddAsync("psi", (_, _) => Task.FromResult("old"), Cancellation);

        cache.Remove("psi");
        var value = await cache.GetOrAddAsync("psi", (_, _) => Task.FromResult("new"), Cancellation);

        Assert.Equal("new", value);
    }

    [Fact]
    public async Task GetOrAdd_LoadReturnsNull_IsNotCached()
    {
        var cache = new InMemoryCache<int, object?>();
        await cache.GetOrAddAsync(1, (_, _) => Task.FromResult<object?>(null), Cancellation);

        var value = await cache.GetOrAddAsync(1, (_, _) => Task.FromResult<object?>("created since"), Cancellation);

        Assert.Equal("created since", value);
    }

    // The shared memory scenario: 100 requests ask for the same key at the same time, on
    // thread pool threads. Several of them may miss and load, but GetOrAdd keeps only the
    // first value, so all of them get the same instance back.
    //
    // `loads` is shared between those threads too: `loads++` is read-add-write, and two
    // threads can read the same old value. Interlocked.Increment does it as one step.
    [Fact]
    public async Task GetOrAdd_ManyRequestsAtOnce_AllGetTheSameValue()
    {
        var cache = new InMemoryCache<int, object>();
        var loads = 0;
        async Task<object> Load(int key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref loads);
            await Task.Delay(10, cancellationToken);
            return new object();
        }

        var values = await Task.WhenAll(Enumerable.Range(0, 100)
            .Select(_ => Task.Run(() => cache.GetOrAddAsync(1, Load, Cancellation), Cancellation)));

        Assert.All(values, value => Assert.Same(values[0], value));
        Assert.InRange(loads, 1, values.Length);
    }
}
```

`CoolApp.Api.Tests.XUnit/Unit/CachedStudySessionServiceTests.cs`: the decorator reads the repository once, and an update is visible on the next read (the copying fake from Lecture 08 makes that test meaningful):

```csharp
using CoolApp.Caching;
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;
using NSubstitute;

namespace CoolApp.Api.Tests.XUnit.Unit;

// The caching decorator around the real StudySessionService.
public class CachedStudySessionServiceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static StudySession Session(string topic) => new()
    {
        Course = "Software Development I",
        Topic = topic,
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };

    // A mock repository: counts how often the session is really read.
    [Fact]
    public async Task GetById_Twice_ReadsTheRepositoryOnce()
    {
        var repository = Substitute.For<IStudySessionRepository>();
        repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(Session("Caching"));
        var service = new CachedStudySessionService(new StudySessionService(repository), new InMemoryCache<int, StudySession?>());

        await service.GetByIdAsync(7, Cancellation);
        await service.GetByIdAsync(7, Cancellation);

        await repository.Received(1).GetByIdAsync(7, Arg.Any<CancellationToken>());
    }

    // The copying fake hands out copies, like a database: without the cache.Remove in
    // UpdateAsync, the second read would still return the cached "Before" copy.
    [Fact]
    public async Task Update_NextGetById_ReturnsTheChange()
    {
        var service = new CachedStudySessionService(
            new StudySessionService(new CopyingStudySessionRepository()), new InMemoryCache<int, StudySession?>());
        var created = await service.CreateAsync(Session("Before"), Cancellation);
        await service.GetByIdAsync(created.Id, Cancellation);

        await service.UpdateAsync(created.Id, Session("After"), Cancellation);
        var read = await service.GetByIdAsync(created.Id, Cancellation);

        Assert.Equal("After", read!.Topic);
    }
}
```

Checkpoint:

```pwsh
dotnet build
dotnet test
```

All 37 tests pass: 31 from step 10, 4 for the cache and 2 for the decorator. Run the app (`dotnet run --project CoolApp.Api`) and go through `CoolApp.Api/CoolApp.http`: the over-posting request now returns `"version": 0`, and reading, updating and reading a session again shows the change.

## 15. Optional: Mutation Testing With Stryker.NET

Coverage says which lines ran during the tests; mutation testing checks whether the tests would **notice** a bug (theory lecture, "Mutation testing"). Stryker.NET changes the code in small ways (`<=` to `<`, removes a statement) and runs the tests against each change. Add it to the local tool manifest, then run it from the test project's folder, limited to this lecture's code:

```pwsh
dotnet tool install dotnet-stryker --version 5.0.0
cd CoolApp.Api.Tests.XUnit
dotnet stryker --test-runner mtp --project CoolApp.Api.csproj --mutate "**/Caching/*.cs" --mutate "**/Services/CachedStudySessionService.cs" --mutate "**/Dtos/*.cs"
```

It takes about 20 seconds and opens nothing by itself: the HTML report is in `StrykerOutput/<date>/reports/` (add `StrykerOutput/` to `.gitignore`). On StudySpot the score is about 56%. The survivors are worth reading: removing `cache.Remove(id)` from `DeleteAsync` or `RateAsync` isn't noticed by any test (*NoCoverage*), and `StartsAt <= now` becoming `StartsAt < now` survives because no test sends a session that starts exactly now. Each survivor is a test you could write.

## The Same Tests in NUnit and MSTest

The side projects have the same 37 tests, with the same changes. They also get the analyzers from `Directory.Build.props`, which found one more rule there, `CA1861` (constant arrays in assertions), excluded for test projects in `.editorconfig`:

```pwsh
dotnet test --project CoolApp.Api.Tests.NUnit
dotnet test --project CoolApp.Api.Tests.MSTest
```

## Checklist for Your Own Project

- **The build checks the standard**: `Directory.Build.props` with `AnalysisLevel`, `EnforceCodeStyleInBuild` and `TreatWarningsAsErrors`, and every rule you switch off has a comment saying why.
- **No entity crosses the API boundary**: every action takes a request DTO and returns a response DTO, for every entity, not just one. The tests check that a field a client must not choose is ignored.
- **Your generic type is used with a real type argument**, from real code, not only declared. A constraint (`where T : ...`) says what it needs from `T`.
- **Your shared memory is really shared** (a singleton, a static field, or something parallel threads touch), and it's protected by a concurrent collection or a `lock` - with a test that uses it from many threads at once.
- **Think about invalidation**: every change goes through a path that removes the cached value.

## Next Steps

Update `ROADMAP.md` (the Beta items for DTOs, a generic type and the shared memory scenario are done) and `README.md`'s "Current Examples". Still open from the Beta list: end-to-end tests with Playwright, which run the React app in a real browser against the running API.

See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
