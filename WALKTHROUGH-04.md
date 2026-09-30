# Walkthrough 04: C# Language Features, and a Real Timezone Bug

This is the step-by-step walkthrough behind [Lecture 04](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture04): closing out the rest of the Alpha requirement checklist (a `record`, an `enum`, named/optional arguments, an extension method, LINQ, and a standard .NET interface) using this week's C# Basics theory, fixing a real timezone bug using this week's Time theory, and a few follow-on additions that came out of the same lecture: a page for the summary endpoint, a `Rating` struct previewing Final's ratings feature, a real filter feature that gives named/optional arguments genuine (not just illustrative) call sites, giving that rating a place in the UI (both to see, on the summary page, and to set, on the main sessions table), and filter controls on both pages that make the two endpoints' different parameters visible to an actual user, not just to someone reading the controller. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

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
- `asOf` is an **optional argument** (`= null`) - call `sessions.UpcomingOnly()` for "right now," or `sessions.UpcomingOnly(asOf: someFixedDate)` (a **named argument**) when you need a fixed, repeatable point in time instead of the real clock. Step 8 below gives this a genuine caller (a `?asOf=...` query parameter), not just a hypothetical one.

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

    public int CompareTo(StudySession? other)
    {
        if (other is null)
        {
            // By convention, any non-null instance sorts after a null one.
            return 1;
        }

        if (StartsAt < other.StartsAt)
        {
            return -1;
        }

        if (StartsAt > other.StartsAt)
        {
            return 1;
        }

        return 0;
    }
}
```

`IComparable<T>.CompareTo`'s contract only cares about the *sign* of the result, never its exact magnitude: negative means "this instance sorts before `other`," zero means "equal for sorting purposes," positive means "this instance sorts after `other`." Writing it with explicit branches - rather than the shorter `StartsAt.CompareTo(other?.StartsAt ?? default)`, which would technically work - keeps that -1/0/1 mapping visible here instead of hidden inside `DateTimeOffset`'s own `CompareTo`.

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

## 6. Add a Session Summaries Page

The `summary` endpoint from step 2 has no consumer yet besides `CoolApp.http`. Add one: `ClientApp/src/components/SessionSummaries.js`:

```jsx
import React, { Component } from 'react';

export class SessionSummaries extends Component {
  static displayName = SessionSummaries.name;

  constructor(props) {
    super(props);
    this.state = { summaries: [], loading: true, error: null };
  }

  componentDidMount() {
    this.populateSummaries();
  }

  static renderSummariesTable(summaries) {
    return (
      <table className="table table-striped" aria-labelledby="tableLabel">
        <thead>
          <tr>
            <th>Course</th>
            <th>Topic</th>
            <th>Starts at</th>
            <th>Seats left</th>
          </tr>
        </thead>
        <tbody>
          {/* StudySessionSummary is an immutable record with no Id, so there's no
              stable identity to key on besides its position in the list. */}
          {summaries.map((summary, index) =>
            <tr key={index}>
              <td>{summary.course}</td>
              <td>{summary.topic}</td>
              <td>{new Date(summary.startsAt).toLocaleString()}</td>
              <td>{summary.seatsAvailable}</td>
            </tr>
          )}
        </tbody>
      </table>
    );
  }

  render() {
    let contents = this.state.loading
      ? <p><em>Loading...</em></p>
      : this.state.error
        ? <p className="text-danger">{this.state.error}</p>
        : SessionSummaries.renderSummariesTable(this.state.summaries);

    return (
      <div>
        <h1 id="tableLabel">Session Summaries</h1>
        <p>A lightweight view of upcoming study sessions from <code>GET /api/studysessions/summary</code> - just course, topic, start time, and seats left, no host name or id.</p>
        {contents}
      </div>
    );
  }

  async populateSummaries() {
    const response = await fetch('api/studysessions/summary');
    if (!response.ok) {
      this.setState({ loading: false, error: 'Could not load session summaries. Please try again.' });
      return;
    }

    const data = await response.json();
    this.setState({ summaries: data, loading: false });
  }
}
```

Wire it up: add `{ path: '/session-summaries', element: <SessionSummaries /> }` to `AppRoutes.js` (alongside the existing `/study-sessions` route), and a matching `<NavLink tag={Link} className="text-dark" to="/session-summaries">Summaries</NavLink>` to `NavMenu.js`.

## 7. Add a Rating Value Type

A struct hasn't shown up in this codebase yet - `SessionStatus` is an `enum`, `StudySessionSummary` a `record`. Add one, previewing Final's "Session ratings" feature. `Models/Rating.cs`:

```csharp
namespace CoolApp.Models;

// A plain struct, not a record struct like StudySessionSummary: it deliberately has
// no built-in `==` (structs only get member-wise Equals by default, per this week's
// theory) - comparing two Ratings needs .Equals(), not ==.
//
// The constructor validates on the way in - but a real struct caveat is worth being
// honest about: C# always gives a struct an implicit public parameterless constructor
// (default(Rating), or new Rating[n]) producing Value = 0, bypassing this constructor
// entirely. No struct can fully prevent that; it's a language-level guarantee, not a
// bug here. In this codebase HostRating is only ever assigned from RatingSubmission's
// already-validated int (see [BindNever] below), so the gap never actually opens in
// practice - but it's real, and worth knowing about before relying on "the constructor
// always runs" for any other struct.
public readonly struct Rating
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value < 1 || value > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Rating must be between 1 and 5.");
        }

        Value = value;
    }

    public override string ToString() => $"{Value}/5";
}
```

Add the request shape it's built from, `Models/RatingSubmission.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CoolApp.Models;

// Validated at the API boundary with the same [Range] pattern as everywhere else in
// this controller, so a bad request gets a clean 400 - rather than relying on Rating's
// own constructor guard (further down the call stack) to reject it with a raw exception.
public record RatingSubmission([Range(1, 5)] int Value);
```

Give `StudySession` a nullable `HostRating`:

```csharp
// [BindNever] keeps this out of Create/Update's model binding entirely - it's
// only ever set server-side, via the rating endpoint below, not by a client
// just including "hostRating" in a POST/PUT body.
[BindNever]
public Rating? HostRating { get; set; }
```

(`[BindNever]` is `Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute` - add the `using` for it.)

And the endpoint, in `StudySessionsController.cs`:

```csharp
[HttpPut("{id:int}/rating")]
public IActionResult RateSession(int id, RatingSubmission submission)
{
    var existing = Sessions.FirstOrDefault(s => s.Id == id);
    if (existing is null)
    {
        return NotFound();
    }

    existing.HostRating = new Rating(submission.Value);
    return NoContent();
}
```

Add `.http` examples: `PUT .../api/studysessions/1/rating` with `{ "value": 4 }` (valid), and with `{ "value": 7 }` (expect `400 Bad Request`).

## 8. Filter Sessions, and Use Named/Optional Arguments for Real

Two things so far have been true only in theory: step 3's `asOf` optional argument has never been called with a real value (every call site just omits it), and nothing yet forces a named argument - it's always been a readability choice. Fix both by adding a real filter, which also happens to close out Beta's "Filter sessions by course" feature early.

Add `Filter` to `Extensions/StudySessionExtensions.cs`:

```csharp
// Two independent, optional filters. Skipping the first to reach only the second
// (Filter(minSeatsAvailable: 2)) isn't just a style choice - C# has no positional
// syntax to "skip" an argument, so naming is the only way to reach a later optional
// parameter without also committing to (and hardcoding) an earlier one's default.
public static IEnumerable<StudySession> Filter(
    this IEnumerable<StudySession> sessions,
    string? course = null,
    int? minSeatsAvailable = null)
{
    if (course is not null)
    {
        sessions = sessions.Where(session => session.Course.Contains(course, StringComparison.OrdinalIgnoreCase));
    }

    if (minSeatsAvailable is not null)
    {
        sessions = sessions.Where(session => session.SeatsAvailable >= minSeatsAvailable);
    }

    return sessions;
}
```

Wire it into both endpoints, but *differently on purpose*:

```csharp
[HttpGet]
public IEnumerable<StudySession> GetAll(
    [FromQuery] string? course = null,
    [FromQuery] int? minSeatsAvailable = null,
    [FromQuery] DateTimeOffset? asOf = null)
{
    var upcoming = Sessions
        .UpcomingOnly(asOf: asOf)
        .Filter(course, minSeatsAvailable)
        .ToList();
    upcoming.Sort();
    return upcoming;
}

[HttpGet("summary")]
public IEnumerable<StudySessionSummary> GetSummaries([FromQuery] int? minSeatsAvailable = null) =>
    Sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
        .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable));
```

The contrast is the point:

- `GetAll` calls `.UpcomingOnly(asOf: asOf)` with a **real, non-default value** now - pass `?asOf=2026-10-15T00:00:00Z` to preview what's upcoming at a future moment instead of right now. It also calls `Filter(course, minSeatsAvailable)` **positionally** - both values are supplied in `Filter`'s own declared order, so naming them would be a readability choice, not a requirement.
- `GetSummaries` deliberately never exposes a course filter, only `minSeatsAvailable`. `Filter(minSeatsAvailable: minSeatsAvailable)` **skips `course` entirely** - `Filter`'s first parameter. There is no positional way to write that call: you'd have to either name `minSeatsAvailable`, or pass `null` explicitly for `course` first. This is the real "named arguments aren't just visibility" moment - it's the only way to reach a later optional parameter without also committing to (and hardcoding) the one you're skipping.

Add a few `.http` examples: `GET .../api/studysessions?course=software`, `?minSeatsAvailable=4`, `?asOf=2026-10-15T00:00:00Z`, and `GET .../api/studysessions/summary?minSeatsAvailable=4`.

## 9. Show Ratings in the Summary View, and Add a Way to Give One

Two loose ends from steps 6 and 7: the summary endpoint doesn't include `HostRating` yet, and nothing in the UI can actually call the rating endpoint - `CoolApp.http` is the only way to rate a session so far.

**Show it, read-only, in the summary.** `StudySessionSummary` predates `Rating` (step 2 came before step 7), so it never had a rating field to include. Add one now:

```csharp
public record StudySessionSummary(string Course, string Topic, DateTimeOffset StartsAt, int SeatsAvailable, Rating? HostRating);
```

Pass it through in `GetSummaries`:

```csharp
Sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
    .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));
```

And render it in `SessionSummaries.js`'s table (a `Rating` column showing `4/5` or `"Not rated"`). This record still has no `Id` - it can *show* a rating, but there's nothing here to act *on*, which is exactly why the interactive part goes elsewhere.

**Give one, from the Study Sessions table.** `StudySessions.js` already has each row's real `id`, so that's where "rate this session" belongs. Add a `Rating` column with the current value plus a `<select>`:

```jsx
static renderSessionsTable(sessions, onRate) {
  return (
    <table className="table table-striped" aria-labelledby="tableLabel">
      <thead>
        <tr>
          {/* ...existing headers... */}
          <th>Rating</th>
        </tr>
      </thead>
      <tbody>
        {sessions.map(session =>
          <tr key={session.id}>
            {/* ...existing cells... */}
            <td>
              {session.hostRating ? `${session.hostRating.value}/5` : 'Not rated'}
              {' '}
              <select
                aria-label={`Rate "${session.topic}"`}
                defaultValue=""
                onChange={event => onRate(session.id, event.target.value)}
              >
                <option value="" disabled>Rate...</option>
                <option value="1">1</option>
                <option value="2">2</option>
                <option value="3">3</option>
                <option value="4">4</option>
                <option value="5">5</option>
              </select>
            </td>
          </tr>
        )}
      </tbody>
    </table>
  );
}
```

`renderSessionsTable` gains a second parameter, `onRate` - a callback threaded down from `render()` (`StudySessions.renderSessionsTable(this.state.sessions, this.rateSession)`), since the method is `static` and has no `this` of its own to call an instance method from directly. The handler itself:

```js
async rateSession(id, value) {
  if (!value) {
    return;
  }

  const response = await fetch(`api/studysessions/${id}/rating`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ value: Number(value) })
  });

  if (response.ok) {
    await this.populateStudySessions();
  }
}
```

(Bind it in the constructor alongside `handleChange`/`handleSubmit`.) The `<select>` is uncontrolled (`defaultValue`, not `value`) - React reuses each row's DOM node across re-renders since `key={session.id}` stays stable, so after picking a rating it keeps showing what was picked rather than snapping back to the placeholder. The number displayed to its left, refreshed by `populateStudySessions()`, is the actual persisted state; the `<select>` is just the input control.

## 10. Add Filter Controls to Both Pages

Step 8's `course`/`minSeatsAvailable`/`asOf` and the summary endpoint's `minSeatsAvailable` have only been exercised via `.http` requests so far. Give each page real controls for its own endpoint's actual parameters - not the same form copy-pasted twice, since the two endpoints don't accept the same things.

**`StudySessions.js`** gets all three, mirroring `GetAll` exactly:

```js
static buildSessionsQuery(filters) {
  const params = new URLSearchParams();

  if (filters.course.trim()) {
    params.set('course', filters.course.trim());
  }

  if (filters.minSeatsAvailable !== '') {
    params.set('minSeatsAvailable', filters.minSeatsAvailable);
  }

  if (filters.asOf) {
    params.set('asOf', new Date(filters.asOf).toISOString());
  }

  const query = params.toString();
  return query ? `?${query}` : '';
}
```

Same timezone reasoning as the create-session form applies to `asOf`: a `datetime-local` input has no offset, so it's converted to an explicit UTC instant before it becomes a query string, not left for the server to guess. Add a small filter form above the table (three inputs - text, number, datetime-local - plus "Apply filters"/"Clear" buttons), state to hold the three values, and change `populateStudySessions` to build its URL from them:

```js
async populateStudySessions(filters = this.state.filters) {
  const query = StudySessions.buildSessionsQuery(filters);
  const response = await fetch(`api/studysessions${query}`);
  const data = await response.json();
  this.setState({ sessions: data, loading: false });
}
```

`populateStudySessions` takes `filters` as a parameter with `this.state.filters` as its default, rather than always reading `this.state` directly - `handleFilterClear` needs to fetch with the just-cleared values immediately, and `setState` doesn't update `this.state` synchronously, so reading it right after calling `setState` would still see the old values.

**`SessionSummaries.js`** gets only `minSeatsAvailable` - a single number input, "Apply"/"Clear" buttons, no course field at all, with a line of UI copy saying so directly: *"No course filter here on purpose - `GetSummaries` only ever declared `minSeatsAvailable`."* This is step 8's named-argument point made visible to an actual user of the page, not just to someone reading the controller.

## 11. Try It

Restart the app (the model changes need a rebuild):

- **The enum**: use `CoolApp.http` to `POST` a session with `"seatsAvailable": 0`, then `GET /api/studysessions` and confirm its `status` reads `"Full"` while the others read `"Scheduled"`.
- **The summary endpoint**: `GET /api/studysessions/summary` and confirm you get back `course`/`topic`/`startsAt`/`seatsAvailable`/`hostRating` - no `id`, no `hostName`. Open `/session-summaries` in the browser and confirm the same data renders as a table, rating column included.
- **Filtering and sorting**: create a few sessions with different `startsAt` values and confirm `GET /api/studysessions` always comes back sorted soonest-first, regardless of the order you created them in. Try `?course=...` and `?minSeatsAvailable=...` against both `/api/studysessions` and `/api/studysessions/summary` (the latter only accepts `minSeatsAvailable` - confirm a `?course=...` on it is simply ignored, since `GetSummaries` never declared that parameter). To see the time-filtering side specifically: the model's own validation correctly refuses a past `startsAt` on both `POST` and `PUT`, so there's no supported way to sneak a past session in through the API to prove it disappears - temporarily comment out the `StartsAt <= DateTimeOffset.UtcNow` check in `Validate`, `POST` a session dated last year, confirm it's missing from `GetAll` but still present if you call `Sessions` directly (e.g. via a quick `GetById`), then restore the check.
- **`asOf`**: `GET /api/studysessions?asOf=` a date far enough in the future that today's seeded sessions have already "started" by then, and confirm the list comes back empty (or missing whichever sessions started before that moment).
- **The timezone fix**: open `/study-sessions`, create a session through the form, and confirm it appears with the right local time in the table (`toLocaleString()` on the frontend already converts back to the browser's own timezone for display). Then send a `.http` request with an explicit non-UTC offset, e.g. `"startsAt": "2026-10-24T20:00:00+02:00"`, and one with `Z` for the equivalent UTC instant, and confirm both are treated identically by the validation (same moment in time, regardless of which offset represents it).
- **Ratings**: `PUT` a rating of `4` onto a session via `CoolApp.http`, then `GET` it back and confirm `"hostRating": { "value": 4 }` appears. `PUT` a rating of `7` and confirm `400 Bad Request`. Then do it from the UI instead: open `/study-sessions`, pick a value from a session's "Rate..." dropdown, and confirm the number next to it updates. Open `/session-summaries` and confirm the same rating shows there too, read-only.
- **Filter controls**: on `/study-sessions`, type part of a course name and click "Apply filters" - confirm the table narrows to matching sessions; do the same with a minimum seats value; pick a future `asOf` date and confirm sessions starting before it disappear; click "Clear" and confirm everything comes back. On `/session-summaries`, confirm there's only ever a seats field to filter by - no course input at all - matching what `GetSummaries` actually accepts.

## Next Steps

Once this is done, update `ROADMAP.md`: check off the rest of your Alpha requirement checklist (steps 1-5), note the ratings building block under Final's ratings feature without checking it off (steps 7 and 9 are a preview - who's allowed to rate, preventing duplicate ratings, and rolling ratings up across a host's sessions are still missing), and check off "Filter sessions by course" under Beta's Features (step 8 - a real feature landed early, not just a requirement-coverage checkbox). Update `README.md`'s "Current Examples" section to match. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
