# Walkthrough 04: C# Language Features, and a Real Timezone Bug

This is the step-by-step walkthrough behind [Lecture 04](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture04): closing out the rest of the Alpha requirement checklist (a `record`, an `enum`, named/optional arguments, an extension method, LINQ, and a standard .NET interface) using this week's C# Basics theory, plus fixing a real timezone bug using this week's Time theory. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-03.md](WALKTHROUGH-03.md) - client- and server-side validation on the create-session form. Apply these same steps to **your own team's project**, not just this repository - check `ROADMAP.md`'s "Requirement coverage still needed" list against your own project's actual code first; you may already have some of these covered by different means.

## 1. Add an `enum` for Session Status

Add `Models/SessionStatus.cs`:

```csharp
namespace CoolApp.Models;

public enum SessionStatus
{
    Scheduled,
    Full,
}
```

Then give `StudySession` a read-only property that computes it, instead of leaving every place that cares about "is this session full" to re-check `SeatsAvailable` itself:

```csharp
public SessionStatus Status => SeatsAvailable > 0 ? SessionStatus.Scheduled : SessionStatus.Full;
```

This is the same idea as this week's Open/Closed theory, in miniature: callers ask `session.Status`, not `session.SeatsAvailable == 0`, so if a third status ever shows up later, it changes in one place.

By default, `System.Text.Json` serializes enums as their underlying number (`0`, `1`, ...), which is unreadable in Swagger, `.http` responses, and the UI. Register a converter in `Program.cs` so it serializes the name instead:

```csharp
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
```

`SeatsAvailable` is currently constrained to `[Range(1, 100)]`, which means `Full` (0 seats) could never actually happen through the API - a `SessionStatus` value that's unreachable isn't much of a demo. Widen it to `[Range(0, 100)]`: a host can now list a session as already full (e.g. an informal group not taking more people) even before your project has any real RSVP/seat-tracking feature.

## 2. Add a `record` for Read-Only Summaries

Add `Models/StudySessionSummary.cs`:

```csharp
namespace CoolApp.Models;

public record StudySessionSummary(string Course, string Topic, DateTimeOffset StartsAt, int SeatsAvailable);
```

A `record` is the right tool here: `StudySessionSummary` is a plain data holder with no identity of its own (no `Id`), two summaries with the same values should be considered equal, and the primary-constructor syntax above gives you that equality plus all four properties for free - no hand-written `Equals`/`GetHashCode`, no property boilerplate.

Add an endpoint that returns it, in `Controllers/StudySessionsController.cs`:

```csharp
[HttpGet("summary")]
public IEnumerable<StudySessionSummary> GetSummaries() =>
    Sessions.Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable));
```

(`"summary"` and `"{id:int}"` don't collide - the `:int` route constraint on the other `[HttpGet]` means `GET /api/studysessions/summary` only ever matches this one.)

## 3. Add an Extension Method, With an Optional Argument

Add `Extensions/StudySessionExtensions.cs`:

```csharp
using CoolApp.Models;

namespace CoolApp.Extensions;

public static class StudySessionExtensions
{
    public static IEnumerable<StudySession> UpcomingOnly(this IEnumerable<StudySession> sessions, DateTimeOffset? asOf = null)
    {
        var cutoff = asOf ?? DateTimeOffset.UtcNow;
        return sessions.Where(session => session.StartsAt > cutoff);
    }
}
```

A few things stacked into this one method:

- It's an **extension method** - `this IEnumerable<StudySession> sessions` as the first parameter means any `IEnumerable<StudySession>` gets a `.UpcomingOnly()` method, the same way `.Where()` and `.Select()` are extension methods on `IEnumerable<T>` themselves.
- It uses **LINQ** (`Where`) to do the actual filtering.
- `asOf` is an **optional argument** (`= null`) - call `sessions.UpcomingOnly()` for "right now," or `sessions.UpcomingOnly(asOf: someFixedDate)` (a **named argument**) when you need a fixed, repeatable point in time instead of the real clock, e.g. from a test.

Use it in `GetAll` and `GetSummaries` so the list only ever shows sessions that haven't started yet:

```csharp
[HttpGet]
public IEnumerable<StudySession> GetAll() => Sessions.UpcomingOnly().ToList();

[HttpGet("summary")]
public IEnumerable<StudySessionSummary> GetSummaries() =>
    Sessions.UpcomingOnly().Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable));
```

## 4. Implement a Standard .NET Interface

`GetAll` above returns whatever order `Sessions` happens to be in. Make sessions sortable by implementing `IComparable<StudySession>` on the model itself:

```csharp
public class StudySession : IValidatableObject, IComparable<StudySession>
{
    // ... existing members ...

    public int CompareTo(StudySession? other) => StartsAt.CompareTo(other?.StartsAt ?? default);
}
```

This is a different tool than step 3's LINQ `Where` - `IComparable<T>` is what `List<T>.Sort()` and `Array.Sort()` use when you call them with **no comparer argument**: the type defines its own natural order once, and every built-in sorting method can then use it. Use it in `GetAll`:

```csharp
[HttpGet]
public IEnumerable<StudySession> GetAll()
{
    var upcoming = Sessions.UpcomingOnly().ToList();
    upcoming.Sort();
    return upcoming;
}
```

`Sessions.UpcomingOnly()` (LINQ, filtering) and `upcoming.Sort()` (`IComparable<StudySession>`, ordering) are deliberately two different mechanisms doing two different jobs - LINQ's own `OrderBy(s => s.StartsAt)` would also have sorted this, no interface required, but then you wouldn't have a reason to implement `IComparable<T>` at all. Worth trying both and seeing that they produce the same order, so the distinction is concrete rather than just a definition.

## 5. Fix a Real Wall-Time Bug

Look at `Models/StudySession.cs`'s validation as it stands before this step:

```csharp
public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
{
    if (StartsAt <= DateTime.Now)
    {
        yield return new ValidationResult(
            "Starts at must be in the future.",
            new[] { nameof(StartsAt) });
    }
}
```

`StartsAt` is a bare `DateTime`, and `DateTime.Now` is the *server's* local time. Neither one carries any explicit timezone information tying them together - "in the future" silently means "in the future, according to whatever timezone this specific server process happens to be running in," which is exactly this week's Time theory's point about wall time: a moment in time isn't meaningful without a timezone to interpret it in, and code that drops that context can be wrong in ways that only show up once client and server disagree about what timezone they meant.

Fix it by switching to `DateTimeOffset`, which carries its offset as part of the value itself:

```csharp
public DateTimeOffset StartsAt { get; set; }

// ...

public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
{
    if (StartsAt <= DateTimeOffset.UtcNow)
    {
        yield return new ValidationResult(
            "Starts at must be in the future.",
            new[] { nameof(StartsAt) });
    }
}
```

`DateTimeOffset.UtcNow` is an explicit, unambiguous instant - and `DateTimeOffset` comparisons (`<=`, `CompareTo`, and step 4's `IComparable<StudySession>`) compare the underlying instant regardless of which offset either side happens to carry, so two sessions created from machines in different timezones still sort and compare correctly against each other.

Update the seed data in `StudySessionsController.cs` to match (`DateTime.Now.AddDays(1)` &rarr; `DateTimeOffset.UtcNow.AddDays(1)`, same for the second sample session).

**This alone isn't the full fix.** `<input type="datetime-local">` (used by the "Starts at" field in `StudySessions.js`) gives you a string like `"2026-10-24T18:00"` with *no* timezone info at all - just switching the model's type doesn't add information that was never sent. In `handleSubmit`, convert it to an explicit instant before it goes anywhere near the network:

```js
const payload = { ...this.state.form, startsAt: new Date(this.state.form.startsAt).toISOString() };

const response = await fetch('api/studysessions', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(payload)
});
```

`new Date(...)` parses the bare local-looking string using the *browser's* timezone (which is the one you actually want - the person filling in the form), and `.toISOString()` turns it into a UTC instant with an explicit `Z` suffix (e.g. `"2026-10-24T16:00:00.000Z"`). The `.http` requests need the same treatment - append `Z` to every `startsAt` value in `CoolApp.http`, since a bare `"2026-09-23T18:00:00"` would otherwise be interpreted as *this server's* local time, whatever machine happens to be running it.

## 6. Try It

Restart the app (the model change needs a rebuild):

- **The enum**: use `CoolApp.http` to `POST` a session with `"seatsAvailable": 0`, then `GET /api/studysessions` and confirm its `status` reads `"Full"` while the others read `"Scheduled"`.
- **The summary endpoint**: `GET /api/studysessions/summary` and confirm you get back `course`/`topic`/`startsAt`/`seatsAvailable` only - no `id`, no `hostName`.
- **Filtering and sorting**: create a few sessions with different `startsAt` values and confirm `GET /api/studysessions` always comes back sorted soonest-first, regardless of the order you created them in. To see the filtering side specifically: the model's own validation correctly refuses a past `startsAt` on both `POST` and `PUT`, so there's no supported way to sneak a past session in through the API to prove it disappears - temporarily comment out the `StartsAt <= DateTimeOffset.UtcNow` check in `Validate`, `POST` a session dated last year, confirm it's missing from `GetAll` but still present if you call `Sessions` directly (e.g. via a quick `GetById`), then restore the check.
- **The timezone fix**: open `/study-sessions`, create a session through the form, and confirm it appears with the right local time in the table (`toLocaleString()` on the frontend already converts back to the browser's own timezone for display). Then send a `.http` request with an explicit non-UTC offset, e.g. `"startsAt": "2026-10-24T20:00:00+02:00"`, and one with `Z` for the equivalent UTC instant, and confirm both are treated identically by the validation (same moment in time, regardless of which offset represents it).

## Next Steps

Once this is done, update `ROADMAP.md` to check off the Alpha items you just completed and `README.md`'s "Current Examples" section to match - if you've followed this walkthrough for your own project, this should close out the rest of your Alpha requirement checklist, same as it does here. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
