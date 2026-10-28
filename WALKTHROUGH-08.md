# Walkthrough 08: Automated Tests, then async/await and Cancellation

This is the step-by-step walkthrough behind [Lecture 08](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture08): adding **unit and integration tests** to StudySpot with xUnit v3 and measuring their **coverage**, and then using those tests as a safety net while every layer becomes **async**, with cancellation passed from the HTTP request all the way down to the repository. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-06.md](WALKTHROUGH-06.md): a controller that depends on `IStudySessionService`, a service that depends on `IStudySessionRepository`, and an in-memory repository, all wired up with Dependency Injection. (Lecture 07 was the Deadline 1 presentations and has no walkthrough of its own.) Apply these same steps to **your own team's project**, not just this repository. For the backend it uses the same tools as the [Testing theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/07-testing.qmd) and its word game demo: xUnit v3 on Microsoft.Testing.Platform, `WebApplicationFactory`, `Microsoft.Testing.Extensions.CodeCoverage` and ReportGenerator. For the React frontend it uses Jest, which the project template already includes, plus React Testing Library. It covers two Lab Assignment #2 requirements: "unit and integration tests coverage at least 50%" and "usage of `async`/`await`, no IO is performed synchronously".

The work happens in **two stages, and the project builds and all tests pass at the end of each one**:

1. **Stage 1 - Automated tests (steps 1-10):** the app moved into its own folder, a test project with unit tests for the service, a fake repository, integration tests that go through the real HTTP API, frontend tests for the React form, and a coverage report.
2. **Stage 2 - async/await and cancellation (steps 11-16):** make the repository, the service and the controller async, update the tests, and add a test that proves a cancelled request does no work.

## 0. Why Tests First

So far every change was checked by hand: run the app, click through `CoolApp.http`, compare. That gets slower with every feature, and it is exactly the check people skip when a deadline is close. Stage 2 is a refactor that touches every layer. With tests in place, "nothing changed" is one command instead of fifteen requests.

# Stage 1 - Automated Tests

## 1. Move the App Into Its Own Folder

Until now the repository root *was* the project: `CoolApp.csproj`, `Program.cs` and every source folder sat next to `README.md` and the solution. A test project has to go somewhere, and putting it in a subfolder of the app means the app's project (which compiles every `.cs` file under its folder) would try to compile the tests too. The usual .NET layout avoids that: the solution and the repository-wide files stay at the root, and **each project gets its own folder**, side by side.

```text
CoolApp.sln              <- the solution, at the root
README.md, ROADMAP.md, docs/, .github/, ...
CoolApp/                 <- the app (this step)
    CoolApp.csproj, Program.cs, CoolApp.http, appsettings*.json
    Controllers/, Extensions/, Models/, Pages/, Properties/, Repositories/, Services/
    ClientApp/
CoolApp.Tests/           <- the tests (next step)
```

From the repository root, with the app stopped:

```pwsh
Remove-Item bin, obj -Recurse -Force -ErrorAction SilentlyContinue
mkdir CoolApp
git mv ClientApp Controllers Extensions Models Pages Properties Repositories Services Program.cs CoolApp.csproj CoolApp.http appsettings.json appsettings.Development.json CoolApp/
dotnet sln remove CoolApp.csproj
dotnet sln add CoolApp/CoolApp.csproj
```

- `git mv` (instead of moving the files in Explorer) records the move as renames, so `git log --follow` and blame still show each file's history. Adjust the list to your project's folders. Anything the `.csproj` or `Program.cs` needs goes into `CoolApp/`, and repository-wide files (`README.md`, `.gitignore`, `.github/`, `docs/`) stay at the root.
- The old `bin/` and `obj/` at the root are build output from the old location. Delete them, or they get in the way of the next build.
- Paths *inside* the project don't change: `<SpaRoot>ClientApp\</SpaRoot>` and `Properties/launchSettings.json` are relative to the `.csproj`, and they moved together with it.
- The solution only needs its path to the project updated. `dotnet sln remove`/`add` does that. Editing the one `CoolApp.csproj` path in `CoolApp.sln` to `CoolApp\CoolApp.csproj` by hand works too.
- Update anything else that names paths: `.github/CODEOWNERS` (`/Controllers/` becomes `/CoolApp/Controllers/`), the README's project map, and any CI scripts.

`dotnet build` from the root builds the solution as before. To run the app, name the project: `dotnet run --project CoolApp`, or `cd CoolApp` and then `dotnet run`. Check that it still starts and `CoolApp/CoolApp.http` still works before you go on.

## 2. Add a Test Project

The test project gets its own folder next to the app. Create `CoolApp.Tests/CoolApp.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- xUnit v3 test projects are executables -->
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\CoolApp\CoolApp.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.14" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
    <PackageReference Include="xunit.v3" Version="4.0.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

(`dotnet new install xunit.v3.templates` and `dotnet new xunit3 -o CoolApp.Tests -f net10.0` creates a similar project, but the template targets an older test runner setup. Writing these lines yourself is simpler and matches the theory demo.)

- `xunit.v3` is the test framework. A v3 test project is an executable, which is why `OutputType` is `Exe`.
- `Microsoft.AspNetCore.Mvc.Testing` provides `WebApplicationFactory`, which starts your real app in memory for integration tests.
- `Microsoft.Testing.Extensions.CodeCoverage` collects coverage (step 9).
- The `ProjectReference` (`..\CoolApp\CoolApp.csproj`) points at the app, so the tests can use its classes.

Two more changes:

1. **Add the project to the solution**, so `dotnet build`, `dotnet test` and your IDE all see it:

   ```pwsh
   dotnet sln add CoolApp.Tests/CoolApp.Tests.csproj
   ```

2. **Use the new test runner.** Add `global.json` next to the solution:

   ```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
   ```

   This makes `dotnet test` use Microsoft.Testing.Platform, the runner xUnit v3 is built for. It's also what enables the `--project` and `--coverage` options used below.

## 3. Unit Tests for the Service

Add `CoolApp.Tests/Unit/StudySessionServiceTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// Tests StudySessionService directly - no HTTP, no web server. The "unit" is the service
// together with the real InMemoryStudySessionRepository: it is fast and deterministic, so
// there is no reason to replace it with a test double.
public class StudySessionServiceTests
{
    // A fixed moment far after the repository's sample sessions (which start "tomorrow"),
    // so every test controls exactly which sessions count as upcoming - no real clock.
    private static readonly DateTimeOffset AsOf = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryStudySessionRepository _repository = new();
    private readonly StudySessionService _service;

    // xUnit creates a new instance of the class for every test, so every test gets its
    // own repository and nothing leaks from one test into the next.
    public StudySessionServiceTests()
    {
        _service = new StudySessionService(_repository);
    }

    private StudySession AddSession(string topic, DateTimeOffset startsAt, string course = "Software Development I", int seats = 5) =>
        _service.Create(new StudySession
        {
            Course = course,
            Topic = topic,
            Location = "MIF, room 401",
            StartsAt = startsAt,
            HostName = "Ieva",
            SeatsAvailable = seats
        });

    [Fact]
    public void GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        AddSession("Before the cutoff", AsOf.AddDays(-1));
        AddSession("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = _service.GetUpcoming(course: null, minSeatsAvailable: null, asOf: AsOf);

        // Assert
        Assert.Equal(["After the cutoff"], upcoming.Select(s => s.Topic));
    }

    [Fact]
    public void GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        AddSession("Third", AsOf.AddDays(3));
        AddSession("First", AsOf.AddDays(1));
        AddSession("Second", AsOf.AddDays(2));

        var upcoming = _service.GetUpcoming(course: null, minSeatsAvailable: null, asOf: AsOf);

        Assert.Equal(["First", "Second", "Third"], upcoming.Select(s => s.Topic));
    }

    // One method, three test cases: the course filter is a case-insensitive "contains".
    [Theory]
    [InlineData("software", 1)]
    [InlineData("SOFTWARE DEVELOPMENT", 1)]
    [InlineData("Algorithms", 0)]
    public void GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        AddSession("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = _service.GetUpcoming(course, minSeatsAvailable: null, asOf: AsOf);

        Assert.Equal(expectedCount, upcoming.Count());
    }

    [Fact]
    public void Update_ExistingSession_ChangeIsVisibleThroughGetById()
    {
        var session = AddSession("Old topic", AsOf.AddDays(1));
        var changed = new StudySession
        {
            Course = session.Course,
            Topic = "New topic",
            Location = session.Location,
            StartsAt = session.StartsAt,
            HostName = session.HostName,
            SeatsAvailable = 2
        };

        var updated = _service.Update(session.Id, changed);

        Assert.True(updated);
        var stored = _service.GetById(session.Id)!;
        Assert.Equal("New topic", stored.Topic);
        Assert.Equal(2, stored.SeatsAvailable);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var updated = _service.Update(999, new StudySession());

        Assert.False(updated);
    }

    [Fact]
    public void Rate_ExistingSession_StoresTheRating()
    {
        var session = AddSession("Rated session", AsOf.AddDays(1));

        var rated = _service.Rate(session.Id, 4);

        Assert.True(rated);
        Assert.Equal(new Rating(4), _service.GetById(session.Id)!.HostRating);
    }

    [Fact]
    public void Rate_UnknownId_ReturnsFalse()
    {
        Assert.False(_service.Rate(999, 4));
    }
}
```

Things to notice, all from the theory lecture:

- **Arrange / Act / Assert**, and names in the form `Method_Scenario_ExpectedResult`.
- **No test double where none is needed.** The real `InMemoryStudySessionRepository` is fast and deterministic, so the "unit" is the service together with it. xUnit creates a new instance of the test class for every test, so every test gets a fresh repository.
- **No real clock.** "Upcoming" depends on the current time, and a test that depends on the current time passes today and fails next month. `UpcomingOnly`'s optional `asOf` argument from Lecture 04 pays off here: every test passes a fixed `AsOf` in 2030, so the sample sessions (which start "tomorrow") never get in the way.
- **`[Theory]` + `[InlineData]`**: one method, three test cases for the course filter.
- **Test behavior, not implementation**: the tests only call the service's public methods and check what comes back. Nothing reads the repository's private list.

## 4. A Fake That Guards the Repository Contract

Lecture 06 ended with a warning: the service only works with a repository that returns copies because it calls `repository.Update(...)` after every change. Turn that warning into a test. Add the fake `CoolApp.Tests/Unit/CopyingStudySessionRepository.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Tests.Unit;

// Fake: a second, working implementation of IStudySessionRepository that - like a real
// database - hands out copies. Mutating what GetById returned changes nothing until
// Update is called (the Liskov "gotcha" from Lecture 06, now as a test double).
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
    public StudySession Add(StudySession session) => Copy(_inner.Add(Copy(session)));
    public bool Update(StudySession session) => _inner.Update(Copy(session));
    public bool Delete(int id) => _inner.Delete(id);
}
```

and `CoolApp.Tests/Unit/StudySessionServiceContractTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// The same service, run against a repository that returns copies. These tests only pass
// because the service saves every change through repository.Update - delete that line
// in StudySessionService.Rate and watch this test fail, while the tests in
// StudySessionServiceTests (using the in-memory repository, which hands out the stored
// instance) still pass.
public class StudySessionServiceContractTests
{
    private readonly StudySessionService _service = new(new CopyingStudySessionRepository());

    [Fact]
    public void Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var session = _service.Create(new StudySession
        {
            Course = "Software Development I",
            Topic = "Testing",
            Location = "MIF, room 401",
            StartsAt = new DateTimeOffset(2030, 1, 2, 18, 0, 0, TimeSpan.Zero),
            HostName = "Tomas",
            SeatsAvailable = 3
        });

        _service.Rate(session.Id, 5);

        Assert.Equal(new Rating(5), _service.GetById(session.Id)!.HostRating);
    }
}
```

A **fake** is a working, simplified implementation of a dependency (here: one that behaves like a database by handing out copies). Try it: in `StudySessionService.Rate`, replace `return repository.Update(existing);` with `return true;` and run the tests. `Rate_ExistingSession_StoresTheRating` still passes, and only this test catches the bug. Put the line back.

## 5. Tests for a Value Type

Add `CoolApp.Tests/Unit/RatingTests.cs`. These are small, but they pin down the 1-5 rule from Lecture 04 at its edges:

```csharp
using CoolApp.Models;

namespace CoolApp.Tests.Unit;

public class RatingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_ValueInRange_IsKept(int value)
    {
        var rating = new Rating(value);

        Assert.Equal(value, rating.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Constructor_ValueOutOfRange_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rating(value));
    }
}
```

## 6. Integration Tests Through the Real API

Unit tests can't see routing, model validation, JSON serialization or the DI registrations in `Program.cs`. Integration tests can. Add `CoolApp.Tests/Integration/StudySessionsApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CoolApp.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoolApp.Tests.Integration;

// Starts the real application (Program.cs) in memory with WebApplicationFactory and talks
// to it over HTTP. These test what the unit tests cannot: routing, model validation, JSON
// serialization and the Dependency Injection registrations.
//
// IClassFixture: one application is started for all tests in this class, because starting
// it for every test would be slow. The tests share its in-memory data, so each test
// creates the sessions it needs instead of relying on what another test left behind.
public class StudySessionsApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static object ValidSession(string topic) => new
    {
        course = "Software Development I",
        topic,
        location = "MIF, room 401",
        startsAt = DateTimeOffset.UtcNow.AddDays(7),
        hostName = "Ieva",
        seatsAvailable = 4
    };

    [Fact]
    public async Task Create_ValidSession_Returns201AndCanBeReadBack()
    {
        var response = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Integration testing"), Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await _client.GetFromJsonAsync<StudySession>(response.Headers.Location, Cancellation);
        Assert.Equal("Integration testing", created!.Topic);
    }

    [Fact]
    public async Task Create_StartsInThePast_Returns400()
    {
        var session = new
        {
            course = "Software Development I",
            topic = "Too late",
            location = "MIF, room 401",
            startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            hostName = "Ieva",
            seatsAvailable = 4
        };

        var response = await _client.PostAsJsonAsync("/api/studysessions", session, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/studysessions/999", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsStatusAsText()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("JSON contract"), Cancellation);

        var json = await _client.GetStringAsync(created.Headers.Location, Cancellation);

        // Checks the JSON contract the frontend relies on: the enum is sent as its name
        // (JsonStringEnumConverter in Program.cs), not as a number.
        Assert.Contains("\"status\":\"Scheduled\"", json);
    }

    [Fact]
    public async Task Rate_ValueOutOfRange_Returns400()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Rate me"), Cancellation);

        var response = await _client.PutAsJsonAsync($"{created.Headers.Location}/rating", new { value = 9 }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
```

- `WebApplicationFactory<Program>` runs your real `Program.cs` in memory. There's no port and no browser, and `CreateClient()` returns an `HttpClient` wired straight to it. Since .NET 10, the `Program` class generated for top-level statements is public, so you don't need `public partial class Program { }` any more.
- `IClassFixture` shares one running app between the tests in the class, because starting it for every test would be slow. The tests therefore share its data, so each one creates the sessions it needs and never assumes what's already there.
- `TestContext.Current.CancellationToken` is xUnit's token for the running test. Pass it to every async call: xUnit's analyzer warns if you don't, and it lets a cancelled test run stop quickly.
- `Create_StartsInThePast_Returns400` tests the Lecture 03 validation end to end: `[ApiController]` rejects the request before the controller runs.

## 7. Frontend Tests with Jest and React Testing Library

The React app has its own test runner already: the ASP.NET Core + React template is built on Create React App, which includes **Jest**. `npm test` in `CoolApp/ClientApp/` runs every `*.test.js` file, and the template's `App.test.js` ("renders without crashing") has been there since Lecture 01. Add **React Testing Library**, which renders a component and lets a test use it the way a user would:

```pwsh
cd CoolApp/ClientApp
npm install --save-dev --save-exact @testing-library/react@16.3.3 @testing-library/dom@10.4.2
```

Add `CoolApp/ClientApp/src/components/StudySessions.test.js`, next to the component it tests:

```js
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from './StudySessions';

// Unit tests for the plain validation function: no rendering, no network.
describe('StudySessions.validate', () => {
  const validForm = {
    course: 'Software Development I',
    topic: 'Testing',
    location: 'MIF, room 401',
    startsAt: '2030-01-01T18:00',
    hostName: 'Ieva',
    seatsAvailable: 3
  };

  it('accepts a complete form', () => {
    expect(StudySessions.validate(validForm)).toEqual({});
  });

  it('reports every empty required field', () => {
    const errors = StudySessions.validate({ ...validForm, course: '', topic: ' ', hostName: '' });

    expect(errors).toEqual({
      course: 'Course is required.',
      topic: 'Topic is required.',
      hostName: 'Host name is required.'
    });
  });

  it('rejects a start time in the past', () => {
    const errors = StudySessions.validate({ ...validForm, startsAt: '2020-01-01T18:00' });

    expect(errors.startsAt).toBe('Starts at must be in the future.');
  });
});

// Component tests: render the real component in jsdom (a browser simulated in Node) and
// use it the way a user would - find inputs by their label, buttons by their role.
// fetch is replaced by a Jest mock, so no API has to run.
describe('StudySessions form', () => {
  const json = (status, body) => Promise.resolve({ ok: status < 400, status, json: () => Promise.resolve(body) });
  const existingSession = {
    id: 1, course: 'Software Development I', topic: 'Pull request etiquette', location: 'MIF, room 401',
    startsAt: '2030-01-01T16:00:00Z', hostName: 'Ieva', seatsAvailable: 3, status: 'Scheduled', hostRating: null
  };

  beforeEach(() => {
    global.fetch = jest.fn(() => json(200, [existingSession]));
  });

  afterEach(() => {
    delete global.fetch;
  });

  it('shows client-side errors and does not call the API when the form is empty', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(screen.getByText('Topic is required.')).toBeTruthy();
    // Only the initial GET of the list - no POST was sent.
    expect(global.fetch).toHaveBeenCalledTimes(1);
  });

  it('shows the API\'s validation error next to the field it belongs to', async () => {
    render(<StudySessions />);
    await screen.findByText('Pull request etiquette');
    fireEvent.change(screen.getByLabelText('Course'), { target: { value: 'Software Development I' } });
    fireEvent.change(screen.getByLabelText('Topic'), { target: { value: 'Testing' } });
    fireEvent.change(screen.getByLabelText('Location'), { target: { value: 'MIF, room 401' } });
    fireEvent.change(screen.getByLabelText('Starts at'), { target: { value: '2030-01-01T18:00' } });
    fireEvent.change(screen.getByLabelText('Host name'), { target: { value: 'Ieva' } });
    global.fetch.mockImplementationOnce(() =>
      json(400, { errors: { Location: ['Location is already booked at that time.'] } }));

    fireEvent.click(screen.getByRole('button', { name: 'Create session' }));

    expect(await screen.findByText('Location is already booked at that time.')).toBeTruthy();
    expect(global.fetch).toHaveBeenLastCalledWith('api/studysessions', expect.objectContaining({ method: 'POST' }));
  });
});
```

- The first `describe` block is **unit tests** for `StudySessions.validate` from Lecture 03. It's a plain function, so the tests just call it and check what comes back. No rendering, no network.
- The second block is **component tests**: `render` puts the real component into jsdom, a browser simulated inside Node. The tests find inputs **by their label** and the button **by its role and name**, as a user or a screen reader would. This is the same idea the theory lecture's Playwright E2E tests use. A test that finds elements by CSS class breaks the moment someone restyles the form.
- `fetch` is replaced by a **Jest mock** (`jest.fn`), so no API has to run. The first test checks that the client-side validation stops the `POST` (only the initial `GET` was called). The second makes the mock answer the `POST` with a 400 and checks that the server's message appears next to the right field. That's the "same errors whether they come from the browser or the API" behavior from Lecture 03.

Run them from `CoolApp/ClientApp/`:

```pwsh
npm test
```

All 6 tests pass: 5 here plus the template's `App.test.js`. `npm test -- --coverage` prints a coverage table for the JavaScript code. The .NET coverage report from step 9 only measures C#. Then go back to the repository root (`cd ../..`).

These tests check the React code on its own. They can't tell whether the frontend and the real API still agree on the JSON. That's what **end-to-end** tests do: the theory lecture's Playwright tests drive a real browser against the running app. They come in a later lecture.

## 8. Run the Tests

```pwsh
dotnet test
```

All 20 .NET tests should pass in a few seconds (`dotnet test` runs only the .NET tests, and `npm test` runs the frontend ones). Then **break something on purpose** and run them again. In `StudySession.CompareTo`, swap the `-1` and the `1`: exactly one test fails, `GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime`, and its name tells you what broke. Undo that, and in `StudySessionExtensions.Filter` change `>= minSeatsAvailable` to `> minSeatsAvailable`. This time **nothing fails**, because no test uses the seats filter. That's a real gap, and the coverage report in the next step shows it as red lines. Undo it, and add a `[Theory]` for the seats filter to your own project's tests.

## 9. Measure Coverage

Install ReportGenerator as a local tool. If you already have `dotnet-tools.json` from an earlier lecture, skip `dotnet new tool-manifest`:

```pwsh
dotnet new tool-manifest
dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.12
```

Collect coverage while the tests run, then turn it into an HTML report:

```pwsh
dotnet test --project CoolApp.Tests --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
dotnet reportgenerator -reports:TestResults/coverage.cobertura.xml -targetdir:coverage-report -assemblyfilters:+CoolApp "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated*;-System.Runtime.CompilerServices*"
```

Open `coverage-report/index.html`. StudySpot is at about **79% line coverage**. Run the second command again without the `-classfilters` part and you'll see about **25%**. The difference is code generated by the compiler and the OpenAPI package, which nobody writes or tests. Excluding generated code, as the theory lecture recommends, is what makes the number mean something. Click into a class to see exactly which lines ran: `StudySessionSummary` and the error page are still at 0%.

Coverage only shows that a line *ran*, not that a test *checked* its result. A test without a single `Assert` raises coverage too. Use the number to find untested code, not to prove the code is correct.

The report is generated output, so add it to `.gitignore` (`TestResults/` is already ignored by the standard Visual Studio `.gitignore`):

```gitignore
# Coverage reports (Lecture 08) - generated by `dotnet reportgenerator`, never committed
coverage-report/
```

## 10. Checkpoint

```pwsh
dotnet build
dotnet test
cd CoolApp/ClientApp
npm test
cd ../..
```

The app builds and all tests pass. Apart from its new location, nothing about the app itself changed in this stage, so `dotnet run --project CoolApp` and `CoolApp.http` behave exactly as before.

# Stage 2 - async/await and Cancellation

Today's repository keeps sessions in a list, but a real one talks to a database, a file or another service: I/O. Synchronous code **blocks** a thread while it waits for I/O. With `async`/`await`, the thread goes back to the pool and serves other requests, and the method continues when the answer arrives. Making the repository interface async now means a database-backed implementation can be swapped in later without touching any caller.

`async` spreads upward: once the repository is async, the service has to `await` it, so the service is async, and so is the controller. Go from the bottom up. **The build stays red from step 11 until step 15**, because each layer still calls the old methods of the layer below. That's normal for this refactor. The tests come back green at step 16, which is why they were written first.

## 11. Make the Repository Interface Async

Replace `CoolApp/Repositories/IStudySessionRepository.cs`:

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
//   - A cancelled token stops the work: the method throws OperationCanceledException
//     instead of reading or changing anything.
//
// Every method is async: storage is I/O (a database, a file, a network call), and a
// thread shouldn't sit blocked while it waits. The CancellationToken is required, not
// `= default`, so the compiler makes every caller pass one along - an optional token is
// easy to forget, and a forgotten token cancels nothing.
public interface IStudySessionRepository
{
    Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken);

    Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken);

    // Assigns the new session's Id and returns the stored session.
    Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);
}
```

- Each method returns a `Task<T>` and gets an `Async` suffix, which is the .NET naming convention for awaitable methods.
- Each method takes a `CancellationToken`. It's a **required** parameter, not `CancellationToken cancellationToken = default`. With a default value, the compiler accepts `GetAllAsync()` without a token. That compiles and runs, but it silently cancels nothing. Making it required means every caller has to decide what to pass.
- Cancellation is now **part of the contract**, next to the rules from Lecture 06.

## 12. Update the In-Memory Repository

Replace `CoolApp/Repositories/InMemoryStudySessionRepository.cs`:

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

    // Nothing here does any I/O, so there is nothing to await: each method does its work
    // right away and wraps the result in an already-completed Task (Task.FromResult) to
    // honor the async interface.
    //
    // It still honors the cancellation part of the contract: a request that was already
    // cancelled gets an OperationCanceledException, exactly as a database-backed
    // implementation would give it, instead of a result nobody is waiting for.
    public Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<StudySession>>(_sessions.ToList());
    }

    // Returns the stored instance itself. That's an implementation detail, not part of
    // the contract (see IStudySessionRepository): callers still have to call Update.
    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));
    }

    public Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        session.Id = _sessions.Count == 0 ? 1 : _sessions.Max(s => s.Id) + 1;
        _sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var index = _sessions.FindIndex(s => s.Id == session.Id);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        _sessions[index] = session;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var existing = _sessions.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(existing is not null && _sessions.Remove(existing));
    }
}
```

A list in memory has nothing to wait for, so there's nothing to `await`. `Task.FromResult` wraps a result that is already known in a completed `Task`. Don't add `async` to a method that has no `await` in it: the compiler warns (CS1998), and you pay for a state machine that does nothing. `cancellationToken.ThrowIfCancellationRequested()` keeps the cancellation promise: an already-cancelled request gets an `OperationCanceledException`, not a result.

## 13. Make the Service Async

Replace `CoolApp/Services/IStudySessionService.cs`:

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
//
// Async all the way up: the repository is async, so everything that calls it is too, and
// each method passes the caller's CancellationToken on to the repository.
public interface IStudySessionService
{
    Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken);

    Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken);

    Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken);

    Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken);

    Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken);
}
```

and `CoolApp/Services/StudySessionService.cs`:

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
    public async Task<IEnumerable<StudySession>> GetUpcomingAsync(string? course, int? minSeatsAvailable, DateTimeOffset? asOf, CancellationToken cancellationToken)
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
        //
        // The await comes first: the filtering and sorting below run in memory, on the
        // list the repository already loaded.
        var sessions = await repository.GetAllAsync(cancellationToken);
        var upcoming = sessions
            .UpcomingOnly(asOf: asOf)
            .Filter(course, minSeatsAvailable)
            .ToList();
        upcoming.Sort();
        return upcoming;
    }

    public async Task<IEnumerable<StudySessionSummary>> GetSummariesAsync(int? minSeatsAvailable, CancellationToken cancellationToken)
    {
        // This endpoint deliberately never exposes a course filter - only minSeatsAvailable.
        // Filter(minSeatsAvailable: minSeatsAvailable) skips `course` (Filter's first
        // parameter) entirely: there is no positional way to write that call. Naming
        // isn't optional polish here, it's the only way to reach the parameter you want
        // without also having to know and restate the one you don't.
        var sessions = await repository.GetAllAsync(cancellationToken);
        return sessions.UpcomingOnly().Filter(minSeatsAvailable: minSeatsAvailable)
            .Select(s => new StudySessionSummary(s.Course, s.Topic, s.StartsAt, s.SeatsAvailable, s.HostRating));
    }

    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        repository.GetByIdAsync(id, cancellationToken);

    public Task<StudySession> CreateAsync(StudySession session, CancellationToken cancellationToken) =>
        repository.AddAsync(session, cancellationToken);

    public async Task<bool> UpdateAsync(int id, StudySession session, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
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
        return await repository.UpdateAsync(existing, cancellationToken);
    }

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);

    public async Task<bool> RateAsync(int id, int value, CancellationToken cancellationToken)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        // [Range] on RatingSubmission.Value already rejected anything outside 1-5
        // before this line runs - the Rating constructor re-checks anyway, since a
        // Rating that's out of range has to be impossible everywhere, not just here.
        existing.HostRating = new Rating(value);
        return await repository.UpdateAsync(existing, cancellationToken);
    }
}
```

The rules don't change. Each method awaits the repository first, and then the same LINQ filtering and sorting runs on the list it got back. The token goes into every repository call.

## 14. Make the Controller Async

Replace `CoolApp/Controllers/StudySessionsController.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Controllers;

// Only translates HTTP to and from IStudySessionService calls: routing, model binding,
// status codes. Everything about what the app actually *does* lives in the service.
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
    public Task<IEnumerable<StudySession>> GetAll(
        CancellationToken cancellationToken,
        [FromQuery] string? course = null,
        [FromQuery] int? minSeatsAvailable = null,
        [FromQuery] DateTimeOffset? asOf = null) =>
        service.GetUpcomingAsync(course, minSeatsAvailable, asOf, cancellationToken);

    [HttpGet("summary")]
    public Task<IEnumerable<StudySessionSummary>> GetSummaries(
        CancellationToken cancellationToken,
        [FromQuery] int? minSeatsAvailable = null) =>
        service.GetSummariesAsync(minSeatsAvailable, cancellationToken);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudySession>> GetById(int id, CancellationToken cancellationToken)
    {
        var session = await service.GetByIdAsync(id, cancellationToken);
        return session is null ? NotFound() : session;
    }

    [HttpPost]
    public async Task<ActionResult<StudySession>> Create(StudySession session, CancellationToken cancellationToken)
    {
        var created = await service.CreateAsync(session, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, StudySession session, CancellationToken cancellationToken) =>
        await service.UpdateAsync(id, session, cancellationToken) ? NoContent() : NotFound();

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken) =>
        await service.DeleteAsync(id, cancellationToken) ? NoContent() : NotFound();

    [HttpPut("{id:int}/rating")]
    public async Task<IActionResult> RateSession(int id, RatingSubmission submission, CancellationToken cancellationToken) =>
        await service.RateAsync(id, submission.Value, cancellationToken) ? NoContent() : NotFound();
}
```

- Add a `CancellationToken cancellationToken` parameter to any action and ASP.NET Core fills it in by itself. It is `HttpContext.RequestAborted`, which is cancelled when the client disconnects (closes the tab, times out, presses Stop). It isn't read from the query string or the body. In `GetAll` and `GetSummaries` it comes before the optional `[FromQuery]` parameters, because C# requires parameters without defaults to come first.
- The action names **keep** their names without `Async` (`GetById`, not `GetByIdAsync`). By default, MVC strips an `Async` suffix from action names, so `CreatedAtAction(nameof(GetByIdAsync), ...)` would point at an action that doesn't exist, and `POST` would fail with "No route matches the supplied values". The integration test `Create_ValidSession_Returns201AndCanBeReadBack` catches exactly that.

## 15. Update the Tests, and Test Cancellation

`dotnet build` now succeeds for the app, but the test project still calls the old synchronous methods. Replace `CoolApp.Tests/Unit/StudySessionServiceTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// Tests StudySessionService directly - no HTTP, no web server. The "unit" is the service
// together with the real InMemoryStudySessionRepository: it is fast and deterministic, so
// there is no reason to replace it with a test double.
public class StudySessionServiceTests
{
    // A fixed moment far after the repository's sample sessions (which start "tomorrow"),
    // so every test controls exactly which sessions count as upcoming - no real clock.
    private static readonly DateTimeOffset AsOf = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryStudySessionRepository _repository = new();
    private readonly StudySessionService _service;

    // xUnit creates a new instance of the class for every test, so every test gets its
    // own repository and nothing leaks from one test into the next.
    public StudySessionServiceTests()
    {
        _service = new StudySessionService(_repository);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private Task<StudySession> AddSessionAsync(string topic, DateTimeOffset startsAt, string course = "Software Development I", int seats = 5) =>
        _service.CreateAsync(new StudySession
        {
            Course = course,
            Topic = topic,
            Location = "MIF, room 401",
            StartsAt = startsAt,
            HostName = "Ieva",
            SeatsAvailable = seats
        }, Cancellation);

    [Fact]
    public async Task GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        await AddSessionAsync("Before the cutoff", AsOf.AddDays(-1));
        await AddSessionAsync("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        // Assert
        Assert.Equal(["After the cutoff"], upcoming.Select(s => s.Topic));
    }

    [Fact]
    public async Task GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        await AddSessionAsync("Third", AsOf.AddDays(3));
        await AddSessionAsync("First", AsOf.AddDays(1));
        await AddSessionAsync("Second", AsOf.AddDays(2));

        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.Equal(["First", "Second", "Third"], upcoming.Select(s => s.Topic));
    }

    // One method, three test cases: the course filter is a case-insensitive "contains".
    [Theory]
    [InlineData("software", 1)]
    [InlineData("SOFTWARE DEVELOPMENT", 1)]
    [InlineData("Algorithms", 0)]
    public async Task GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        await AddSessionAsync("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = await _service.GetUpcomingAsync(course, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.Equal(expectedCount, upcoming.Count());
    }

    [Fact]
    public async Task Update_ExistingSession_ChangeIsVisibleThroughGetById()
    {
        var session = await AddSessionAsync("Old topic", AsOf.AddDays(1));
        var changed = new StudySession
        {
            Course = session.Course,
            Topic = "New topic",
            Location = session.Location,
            StartsAt = session.StartsAt,
            HostName = session.HostName,
            SeatsAvailable = 2
        };

        var updated = await _service.UpdateAsync(session.Id, changed, Cancellation);

        Assert.True(updated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.Equal("New topic", stored.Topic);
        Assert.Equal(2, stored.SeatsAvailable);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsFalse()
    {
        var updated = await _service.UpdateAsync(999, new StudySession(), Cancellation);

        Assert.False(updated);
    }

    [Fact]
    public async Task Rate_ExistingSession_StoresTheRating()
    {
        var session = await AddSessionAsync("Rated session", AsOf.AddDays(1));

        var rated = await _service.RateAsync(session.Id, 4, Cancellation);

        Assert.True(rated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.Equal(new Rating(4), stored.HostRating);
    }

    [Fact]
    public async Task Rate_UnknownId_ReturnsFalse()
    {
        Assert.False(await _service.RateAsync(999, 4, Cancellation));
    }

    // Cancellation is part of the contract too: a request that was already cancelled
    // (the client disconnected) must not do any work.
    [Fact]
    public async Task GetUpcoming_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, cancellation.Token));
    }
}
```

`CoolApp.Tests/Unit/CopyingStudySessionRepository.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Tests.Unit;

// Fake: a second, working implementation of IStudySessionRepository that - like a real
// database - hands out copies. Mutating what GetById returned changes nothing until
// Update is called (the Liskov "gotcha" from Lecture 06, now as a test double).
public class CopyingStudySessionRepository : IStudySessionRepository
{
    private readonly InMemoryStudySessionRepository _inner = new();

    private static StudySession Copy(StudySession s) => new()
    {
        Id = s.Id, Course = s.Course, Topic = s.Topic, Location = s.Location,
        StartsAt = s.StartsAt, HostName = s.HostName, SeatsAvailable = s.SeatsAvailable,
        HostRating = s.HostRating
    };

    public async Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken) =>
        (await _inner.GetAllAsync(cancellationToken)).Select(Copy).ToList();

    public async Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await _inner.GetByIdAsync(id, cancellationToken) is { } s ? Copy(s) : null;

    public async Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken) =>
        Copy(await _inner.AddAsync(Copy(session), cancellationToken));

    public Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken) =>
        _inner.UpdateAsync(Copy(session), cancellationToken);

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        _inner.DeleteAsync(id, cancellationToken);
}
```

and `CoolApp.Tests/Unit/StudySessionServiceContractTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// The same service, run against a repository that returns copies. These tests only pass
// because the service saves every change through repository.UpdateAsync - delete that
// line in StudySessionService.RateAsync and watch this test fail, while the tests in
// StudySessionServiceTests (using the in-memory repository, which hands out the stored
// instance) still pass.
public class StudySessionServiceContractTests
{
    private readonly StudySessionService _service = new(new CopyingStudySessionRepository());

    [Fact]
    public async Task Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var session = await _service.CreateAsync(new StudySession
        {
            Course = "Software Development I",
            Topic = "Testing",
            Location = "MIF, room 401",
            StartsAt = new DateTimeOffset(2030, 1, 2, 18, 0, 0, TimeSpan.Zero),
            HostName = "Tomas",
            SeatsAvailable = 3
        }, TestContext.Current.CancellationToken);

        await _service.RateAsync(session.Id, 5, TestContext.Current.CancellationToken);

        var stored = await _service.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        Assert.Equal(new Rating(5), stored!.HostRating);
    }
}
```

The changes are mechanical: test methods become `async Task`, calls are awaited, and every call passes `TestContext.Current.CancellationToken`. The new test at the end creates a `CancellationTokenSource`, cancels it **before** calling the service, and asserts that the call throws instead of returning sessions. That's the contract from step 11, checked through the service, the way a disconnected client's request would reach it. `ThrowsAnyAsync` accepts `OperationCanceledException` and its subclasses, such as `TaskCanceledException`, which a real database driver may throw instead. The integration tests and `RatingTests` need no changes: they already used the HTTP API asynchronously.

## 16. Checkpoint: Build, Test, Run

```pwsh
dotnet build
dotnet test
```

All 21 tests pass: the same 20 as before the refactor, plus the cancellation test. Then `dotnet run --project CoolApp` and go through `CoolApp/CoolApp.http` once more. Nothing should behave differently: `GET`s, `POST` (201, and a `Location` header pointing at the new id), invalid `POST` (400), `PUT`, ratings, `DELETE`, and the filters.

## Common async Mistakes to Look For in Your Own Code

- **`.Result` or `.Wait()`** on a `Task`: blocks the thread anyway (no benefit from async) and can deadlock. Use `await`.
- **`async void`**: the caller can't await it, and an exception in it crashes the process. Use `async Task`. The only exception is event handlers.
- **A token that never reaches the I/O**: accepted in the controller but not passed further, or a `= default` parameter that every caller skips. Follow one request from the controller to the repository and check that the same token arrives.
- **Tests that ignore async**: a test that calls an async method without `await` (or with `.Result`) can pass while the work never finished. Make the test `async Task` and await.

## Next Steps

Update `ROADMAP.md` (the Beta item "`async`/`await` for all I/O" now has its async API in place; the coverage item has a real number) and `README.md`'s "Project Map" and "Current Examples". Two things are deliberately left for later lectures:

- The repository still keeps everything in a `List` inside a singleton. Two requests that run at the same time share that list, and `List<T>` is not safe for concurrent writes. That's Lab Assignment #2's "shared memory scenario" requirement, and the database removes the shared list altogether.
- End-to-end tests with Playwright, which run the React app in a real browser against the running API.
- Storing sessions in a real database with Entity Framework Core: a second implementation of the now-async `IStudySessionRepository`, swapped in with one line in `Program.cs`, with the tests from this walkthrough checking that nothing else changed.

See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
