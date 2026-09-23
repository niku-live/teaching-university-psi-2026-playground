# Walkthrough 02: Full CRUD, Swagger, and a Create-Session Form

This is the step-by-step walkthrough behind [Lecture 02](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture02): completing full CRUD on the API, adding Swagger/OpenAPI documentation, and adding a real UI form for creating a study session. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-01.md](WALKTHROUGH-01.md) - a `GET`/`POST`-only `StudySessionsController` and a read-only sessions page. Apply these same steps to **your own team's project**, not just this repository.

## 1. Complete Full CRUD on the API

The API so far only supports `GET` and `POST`. Add `PUT` and `DELETE` to `StudySessionsController.cs`, right after the existing `Create` action:

```csharp
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
```

Both mirror `GetById`'s existing `NotFound()` check for a missing id. Both return `NoContent()` (`204`) rather than the resource itself - contrast with `Create`'s `201` + `CreatedAtAction`: a `204` means "succeeded, nothing to send back," which fits an update/delete the caller already knows the shape of.

## 2. Add Swagger/OpenAPI Documentation

> [!NOTE]
> The package versions below were verified against real NuGet registration metadata as compatible with `net10.0` at the time of writing. Check [nuget.org](https://www.nuget.org/) for newer compatible versions before using these in your own project - don't assume they'll still be current.

Add the two packages:

```bash
dotnet add package Microsoft.AspNetCore.OpenApi --version 10.0.8
dotnet add package Swashbuckle.AspNetCore --version 10.2.3
```

`Microsoft.AspNetCore.OpenApi` generates the OpenAPI document from your controllers' attributes and types; `Swashbuckle.AspNetCore` serves the interactive Swagger UI on top of it.

In `Program.cs`, register the services right after the existing `AddControllersWithViews()`:

```csharp
builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
```

Then add the middleware, gated to Development, right after the existing HSTS `if` block:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

Gating behind `IsDevelopment()` matters: nobody wants a public, unauthenticated API explorer sitting on a production deployment by accident.

Run the project and open `https://localhost:7039/swagger` (adjust the port to your own `launchSettings.json`) - every endpoint is listed, including the new `PUT`/`DELETE`, with real request/response schemas generated straight from the controller.

## 3. Create a Study Session From the UI

Until now, creating a session was API-only (`curl`/REST Client/Postman). Add a real form to `StudySessions.js` so it can be done from the app itself.

Add a form state alongside the existing `sessions`/`loading` state:

```js
const emptyForm = {
  course: '',
  topic: '',
  location: '',
  startsAt: '',
  hostName: '',
  seatsAvailable: 1
};

// in the constructor:
this.state = { sessions: [], loading: true, form: { ...emptyForm }, submitting: false, error: null };
this.handleChange = this.handleChange.bind(this);
this.handleSubmit = this.handleSubmit.bind(this);
```

Add controlled inputs for each field (course, topic, location, starts-at, host name, seats available) inside a `<form onSubmit={this.handleSubmit}>`, then wire up the two handlers:

```js
handleChange(event) {
  const { name, value } = event.target;
  this.setState(prevState => ({
    form: { ...prevState.form, [name]: name === 'seatsAvailable' ? Number(value) : value }
  }));
}

async handleSubmit(event) {
  event.preventDefault();
  this.setState({ submitting: true, error: null });

  const response = await fetch('api/studysessions', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(this.state.form)
  });

  if (!response.ok) {
    this.setState({ submitting: false, error: 'Could not create the study session. Please try again.' });
    return;
  }

  this.setState({ form: { ...emptyForm }, submitting: false });
  await this.populateStudySessions();
}
```

`handleSubmit` reuses `populateStudySessions()` (already there for the initial load) to refresh the table after a successful `POST` - see this repo's own [`ClientApp/src/components/StudySessions.js`](ClientApp/src/components/StudySessions.js) for the exact, complete file if you want to match it directly rather than reassembling it from these pieces.

## 4. Test It

Run the project (`dotnet run`), then exercise the full CRUD surface with whichever client you prefer - they're all just HTTP:

- **Swagger UI** at `/swagger` - browse the schema, or use "Try it out" to send a request from the page itself.
- **REST Client** - this repo's own [`CoolApp.http`](CoolApp.http) has the full CRUD sequence ready to send (create one for your own project, adjusting host/paths to match). See the [main course repo's Lecture 02 material](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture02) for a general-purpose REST API primer using public APIs (PokeAPI, JSONPlaceholder), plus what the REST Client extension is and how to install it.
- **PowerShell** (`Invoke-RestMethod`) - the same requests, sent without a `.http` file at all.
- **The browser itself** - `GET` requests work by just typing the URL in the address bar; open Developer Tools' Network tab while using the real `/study-sessions` page to see the `GET`/`POST` requests the UI fires for you.
- **The new form** - open `/study-sessions`, fill it in, submit, and confirm the table refreshes with the new session.

## Next Steps

Once this is done, update `ROADMAP.md` to check off the items you just completed, and `README.md`'s "Current Examples" section to match. Continue with [WALKTHROUGH-03.md](WALKTHROUGH-03.md) - adding real input validation, client and server. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
