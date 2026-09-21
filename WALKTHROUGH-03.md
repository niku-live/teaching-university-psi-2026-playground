# Walkthrough 03: Validating Input, Client and Server

This is the step-by-step walkthrough behind [Lecture 03](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture03): adding real input validation to the create-session form (both client- and server-side), a small CSS pass, and two lightweight Agile process artifacts. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-02.md](WALKTHROUGH-02.md) - full CRUD on the API, Swagger docs, and a create-session form. Apply these same steps to **your own team's project**, not just this repository.

## 1. Add Server-Side Validation to the Model

So far, nothing stops a client from `POST`ing an empty `course`, a negative `seatsAvailable`, or a `startsAt` in the past - the API accepts it. Add `DataAnnotations` to `Models/StudySession.cs`:

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

Try it with the new example in [`CoolApp.http`](CoolApp.http) - `POST`ing an empty course, a zero seat count, and a past `startsAt` all at once now returns `400` with all three errors listed, instead of silently succeeding.

## 2. Add Client-Side Validation to the Form

Server-side validation is required - it's the only check that can't be bypassed. But making the user wait for a round trip just to learn a field was empty is a bad experience. Add a `validate` check in `ClientApp/src/components/StudySessions.js` that runs before the request is even sent:

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

The API can still reject something the client-side check missed (or a request sent by something other than this form). Map the API's `ValidationProblemDetails` shape (`{ errors: { PropertyName: ["message"] } }`) onto the same `fieldErrors` shape the client-side check already produces, so both paths render identically:

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

In `handleSubmit`, when the response is `!response.ok` and `response.status === 400`, parse the JSON body and pass it through `mapServerErrors` into `fieldErrors` instead of the generic error message.

Render each field's error next to its input using Bootstrap's validation classes - `is-invalid` on the `<input>` plus an `.invalid-feedback` div right after it:

```jsx
<input className={`form-control${error ? ' is-invalid' : ''}`} ... />
{error && <div className="invalid-feedback">{error}</div>}
```

See this repo's own [`ClientApp/src/components/StudySessions.js`](ClientApp/src/components/StudySessions.js) for the exact, complete file - it factors the repeated field markup into a small `renderField` helper and adds `noValidate` to the `<form>` so the browser's own native validation UI doesn't fight with the custom one.

## 3. A Small CSS Pass

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

## 4. Add Agile Process Artifacts

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

## 5. Test It

- **Server-side**: run the project (`dotnet run`), then send the new invalid-data example in [`CoolApp.http`](CoolApp.http) - confirm you get `400 Bad Request` with all three field errors listed, and that the earlier valid examples still succeed.
- **Client-side**: open `/study-sessions`, submit the form empty - confirm every required field shows an inline error immediately, with no network request sent (check the Network tab). Fix the fields one at a time and confirm the errors clear as you go.
- **Shared error path**: temporarily comment out the client-side `validate()` call in `handleSubmit` (or use `curl`/REST Client directly) to confirm the server's `400` response still renders through the same `fieldErrors` UI.
- **Layout**: resize the browser window and confirm the form's fields reflow between one and two columns.

## Next Steps

Once this is done, update `ROADMAP.md` to check off "Basic input validation on the `StudySession` model" and `README.md`'s "Current Examples" section to match. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
