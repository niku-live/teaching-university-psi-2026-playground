# Walkthrough 03: Validating Input, Client and Server

This is the step-by-step walkthrough behind [Lecture 03](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture03): adding real input validation to the create-session form (both client- and server-side), a small CSS pass, and two lightweight Agile process artifacts. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-02.md](WALKTHROUGH-02.md) - full CRUD on the API, Swagger docs, and a create-session form. Apply these same steps to **your own team's project**, not just this repository.

The steps below are deliberately ordered as a small security lesson, not just a feature list: add client-side validation first, prove to yourself why that alone is not enough, *then* add server-side validation. If you only ever do the server-side part, you'll still be safe - but seeing the gap for yourself first is worth the extra ten minutes.

## 1. Add Client-Side Validation to the Form

Add a `validate` check in `ClientApp/src/components/StudySessions.js` that runs before the request is even sent:

```js
static validate(form) {
  const errors = {};

  if (!form.course.trim()) errors.course = 'Course is required.';
  if (!form.topic.trim()) errors.topic = 'Topic is required.';
  if (!form.location.trim()) errors.location = 'Location is required.';
  if (!form.hostName.trim()) errors.hostName = 'Host name is required.';

  if (!form.startsAt) {
    errors.startsAt = 'Starts at is required.';
  } else if (new Date(form.startsAt) <= new Date()) {
    errors.startsAt = 'Starts at must be in the future.';
  }

  if (!form.seatsAvailable || form.seatsAvailable < 1) {
    errors.seatsAvailable = 'Seats available must be at least 1.';
  }

  return errors;
}
```

Add `fieldErrors: {}` to the component's initial state, and check `validate()` at the top of `handleSubmit` before firing the request - if there are any errors, set them in state and return early instead of calling `fetch`.

Also add the shape that will handle *server*-reported errors later, even though nothing on the server sends them yet:

```js
static mapServerErrors(problemDetails) {
  const errors = {};
  if (problemDetails && problemDetails.errors) {
    for (const [key, messages] of Object.entries(problemDetails.errors)) {
      const fieldName = key.charAt(0).toLowerCase() + key.slice(1);
      errors[fieldName] = messages[0];
    }
  }
  return errors;
}
```

In `handleSubmit`, when the response is `!response.ok` and `response.status === 400`, parse the JSON body and pass it through `mapServerErrors` into `fieldErrors` instead of the generic error message. This branch is dead code for now - the API doesn't return a `400` yet - but it's ready for the moment it does.

Render each field's error next to its input using Bootstrap's validation classes - `is-invalid` on the `<input>` plus an `.invalid-feedback` div right after it:

```jsx
<input className={`form-control${error ? ' is-invalid' : ''}`} ... />
{error && <div className="invalid-feedback">{error}</div>}
```

See this repo's own [`ClientApp/src/components/StudySessions.js`](ClientApp/src/components/StudySessions.js) for the exact, complete file - it factors the repeated field markup into a small `renderField` helper and adds `noValidate` to the `<form>` so the browser's own native validation UI doesn't fight with the custom one.

Run the project (`dotnet run`) and confirm it works: submit the form empty, see instant inline errors with no network request; fill it in correctly and submit for real.

## 2. Try to Break It: Bypass the Form With a Direct API Call

Here's the part worth actually doing, not just reading. Add this new example to [`CoolApp.http`](CoolApp.http), right after the existing `DELETE` request:

```http
### Create a study session with invalid data (expect 400 Bad Request)
POST {{HostAddress}}/api/studysessions
Content-Type: application/json

{
  "course": "",
  "topic": "Testing validation",
  "location": "MIF, room 401",
  "startsAt": "2020-01-01T18:00:00",
  "hostName": "You",
  "seatsAvailable": 0
}
```

It stacks three violations at once: an empty `course`, a `startsAt` in 2020, and `seatsAvailable: 0`. Send it - with the REST Client extension's "Send Request" link, `curl`, Postman, or anything else that can make an HTTP request.

**It succeeds.** `201 Created`, and the garbage data now has an `id`. Refresh `/study-sessions` in the browser and it's sitting right there in the table: an empty course cell, a session dated in 2020, `0` seats.

This is the point: the form's `validate()` from step 1 never ran, because this request never went through the form. Nothing about the API itself checked anything. Client-side validation only protects people who use the form - `curl`, a browser's own DevTools console, or a script targeting your API directly all walk straight around it. If your team's only validation lives in JavaScript on the frontend, this is exactly how bad data gets into your database anyway.

## 3. Add Server-Side Validation to the Model

Fix the actual gap: add `DataAnnotations` to `Models/StudySession.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace CoolApp.Models;

public class StudySession : IValidatableObject
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Course { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Topic { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Location { get; set; } = string.Empty;

    public DateTime StartsAt { get; set; }

    [Required, StringLength(100)]
    public string HostName { get; set; } = string.Empty;

    [Range(1, 100)]
    public int SeatsAvailable { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartsAt <= DateTime.Now)
        {
            yield return new ValidationResult(
                "Starts at must be in the future.",
                new[] { nameof(StartsAt) });
        }
    }
}
```

`[Required]`, `[StringLength]`, and `[Range]` cover the simple per-field rules. `StartsAt` needs a rule that depends on the current time, which a single attribute can't express - implementing `IValidatableObject` lets the model validate itself against rules that don't fit an attribute.

**You don't need to touch the controller.** `StudySessionsController` is already annotated `[ApiController]`, and that attribute makes ASP.NET Core validate the model automatically before your action method even runs - an invalid `StudySession` never reaches `Create` or `Update`; the framework short-circuits straight to a `400 Bad Request` with a `ValidationProblemDetails` body describing what failed, field by field.

Also fix the existing `PUT` example's `startsAt` in `CoolApp.http` - it was hardcoded to a fixed date that's now stale, and once "must be in the future" is enforced, that request would itself start failing:

```http
"startsAt": "2026-09-24T18:00:00",
```

(Use a date genuinely in the future relative to when you're reading this.)

## 4. Re-run the Attack From Step 2

Restart the app (the model change needs a rebuild), then send the **exact same** invalid `POST` from step 2 again.

This time: `400 Bad Request`, with a body listing every violation - `Course` ("required"), `StartsAt` (the custom "must be in the future" message), `SeatsAvailable` (the range constraint). Nothing changed in the controller between the two attempts. The only difference is the model now validates itself, and `[ApiController]` enforces that automatically for every request, not just ones that happen to come through your form.

One more thing worth noticing: refresh `/study-sessions` and the garbage row from step 2 is gone. That's not validation cleaning up after itself - validation only ever stops a *new* bad write, it can't retroactively fix data that's already stored. It's gone because restarting the app reset this project's in-memory sample data. In a real database-backed app, that row would still be sitting there until someone manually finds and removes it - which is itself the strongest argument for validating before bad data gets in, rather than trying to clean it up afterward.

## 5. A Small CSS Pass

Reinforcing this week's CSS/flexbox material with something visible: add a component-scoped stylesheet, `ClientApp/src/components/StudySessions.css`, and `import './StudySessions.css';` at the top of `StudySessions.js` (same pattern as `NavMenu.js` importing `NavMenu.css`):

```css
.study-sessions-table {
  overflow-x: auto;
}

.study-session-form {
  display: flex;
  flex-wrap: wrap;
  gap: 0 1rem;
  max-width: 40rem;
}

.study-session-form__field {
  flex: 1 1 16rem;
}

.study-session-form button {
  flex-basis: 100%;
}
```

Wrap the table in a `<div className="study-sessions-table">` and add `className="study-session-form"` to the `<form>`. `flex-wrap` plus each field's `flex: 1 1 16rem` lets the form lay out two columns on a wide screen and collapse to one on a narrow one, without a media query.

## 6. Add Agile Process Artifacts

This week's other theory topic was Agile - specifically the idea of a shared "Definition of Done" and collective ownership backed by a shared standard. Turn that into two files your team can actually use, rather than just discussing it:

`.github/pull_request_template.md` - GitHub pre-fills this into every new pull request's description box:

```markdown
## What does this change do?

<!-- One or two sentences. What problem does this solve, or what does it add? -->

## How was it tested?

<!-- Manual steps you took, and/or which automated tests cover this. -->

## Checklist

- [ ] Meets [`docs/definition-of-done.md`](../docs/definition-of-done.md)
- [ ] `ROADMAP.md` updated if this closes an item
```

`docs/definition-of-done.md` - the shared checklist the template links to:

```markdown
# Definition of Done

A change is "done" when all of the following are true:

- [ ] It builds and runs locally (`dotnet build` and `npm run build` both succeed).
- [ ] It has been reviewed and tested - manually at minimum, with an automated test where one makes sense.
- [ ] It is merged to `main` via a pull request, not pushed directly.
- [ ] It matches the pull request template's checklist.
- [ ] Any relevant docs (`README.md`, `ROADMAP.md`) are updated in the same pull request, not left for later.

"Works on my machine" is not done. If it isn't merged and documented, it isn't done.
```

These aren't code your app runs - they're process, same as your branch naming rule or your `dotnet format` habit. Add your own team's equivalents to your own repository this week.

## 7. Final Check

- **The attack, one more time**: confirm the invalid `POST` from step 2 still returns `400` after all the later changes (CSS, Agile artifacts) - nothing in those steps should affect it.
- **The form, end to end**: open `/study-sessions`, submit it empty (instant inline errors, no network request), then submit valid data with a future `startsAt` and confirm the new session appears in the table.
- **Layout**: resize the browser window and confirm the form's fields reflow between one and two columns.

## Next Steps

Once this is done, update `ROADMAP.md` to check off "Basic input validation on the `StudySession` model" and `README.md`'s "Current Examples" section to match. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
