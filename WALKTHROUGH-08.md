# Walkthrough 08: Automated Tests, then async/await and Cancellation

This is the step-by-step walkthrough behind [Lecture 08](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture08): adding **unit, controller and integration tests** to StudySpot with xUnit v3 and measuring their **coverage**, and then using those tests as a safety net while every layer becomes **async**, with cancellation passed from the HTTP request all the way down to the repository. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-06.md](WALKTHROUGH-06.md): a controller that depends on `IStudySessionService`, a service that depends on `IStudySessionRepository`, and an in-memory repository, all wired up with Dependency Injection. (Lecture 07 was the Deadline 1 presentations and has no walkthrough of its own.) Apply these same steps to **your own team's project**, not just this repository. For the backend it uses the same tools as the [Testing theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/07-testing.qmd) and its word game demo: xUnit v3 on Microsoft.Testing.Platform, NSubstitute, `WebApplicationFactory`, `Microsoft.Testing.Extensions.CodeCoverage` and ReportGenerator. For the React frontend it uses Jest, which the project template already includes, plus React Testing Library. It covers two Lab Assignment #2 requirements: "unit and integration tests coverage at least 50%" and "usage of `async`/`await`, no IO is performed synchronously".

The work happens in **two stages, and the project builds and all tests pass at the end of each one**:

1. **Stage 1 - Automated tests (steps 1-11):** the API and the React app moved into folders of their own, a test project with unit tests for the service, a fake repository, controller tests with a mocked service, integration tests that go through the real HTTP API, frontend tests for the React form in their own folder, and one coverage report for the backend and the frontend.
2. **Stage 2 - async/await and cancellation (steps 12-18):** make the repository, the service and the controller async, update the tests, add a test that proves a cancelled request does no work, and finally run the app once more by hand.

## 0. Why Tests First

So far every change was checked by hand: run the app, click through `CoolApp.http`, compare. That gets slower with every feature, and it is exactly the check people skip when a deadline is close. Stage 2 is a refactor that touches every layer. With tests in place, "nothing changed" is one command instead of fifteen requests.

# Stage 1 - Automated Tests

## 1. Give the API and the React App Their Own Folders

Until now the repository root *was* the project: `CoolApp.csproj`, `Program.cs` and every source folder sat next to `README.md` and the solution, and the React app was a subfolder of it (`ClientApp/`). Tests have to go somewhere, and putting a test project in a subfolder of the app means the app's project (which compiles every `.cs` file under its folder) would try to compile the tests too. The usual .NET layout avoids that: the solution and the repository-wide files stay at the root, and **each project gets its own folder**, side by side. That includes the React app, which is a separate program with its own `package.json`, and doesn't need to live inside the API project either.

```text
CoolApp.sln              <- the solution, at the root
README.md, ROADMAP.md, docs/, .github/, ...
CoolApp.Api/             <- the ASP.NET Core API (this step)
    CoolApp.Api.csproj, Program.cs, CoolApp.http, appsettings*.json
    Controllers/, Extensions/, Models/, Pages/, Properties/, Repositories/, Services/
CoolApp.Client/          <- the React app (this step)
    package.json, src/, public/
CoolApp.Api.Tests.XUnit/ <- the API tests (next step)
```

From the repository root, with the app stopped:

```pwsh
Remove-Item bin, obj -Recurse -Force -ErrorAction SilentlyContinue
mkdir CoolApp.Api
git mv Controllers Extensions Models Pages Properties Repositories Services Program.cs CoolApp.csproj CoolApp.http appsettings.json appsettings.Development.json CoolApp.Api/
git mv CoolApp.Api/CoolApp.csproj CoolApp.Api/CoolApp.Api.csproj
git mv ClientApp CoolApp.Client
dotnet sln remove CoolApp.csproj
dotnet sln add CoolApp.Api/CoolApp.Api.csproj
```

Then two lines in `CoolApp.Api/CoolApp.Api.csproj`. Replace `<SpaRoot>ClientApp\</SpaRoot>` with:

```xml
    <!-- The code keeps its CoolApp.* namespaces; without this, new files would get CoolApp.Api.* -->
    <RootNamespace>CoolApp</RootNamespace>
    <!-- The React app is a sibling folder, not a subfolder of the API project -->
    <SpaRoot>..\CoolApp.Client\</SpaRoot>
```

- `git mv` (instead of moving the files in Explorer) records the move as renames, so `git log --follow` and blame still show each file's history. Adjust the list to your project's folders. Anything the `.csproj` or `Program.cs` needs goes into `CoolApp.Api/`, and repository-wide files (`README.md`, `.gitignore`, `.github/`, `docs/`) stay at the root.
- The old `bin/` and `obj/` at the root are build output from the old location. Delete them, or they get in the way of the next build. If `ClientApp/node_modules` exists, it moves along with the React app.
- Renaming the project file to `CoolApp.Api.csproj` also renames the assembly (`CoolApp.Api.dll`). By default it would change the namespace for new files too, so `RootNamespace` keeps it at `CoolApp`. The existing code keeps its `namespace CoolApp...` lines, and no `.cs` file has to change.
- `SpaRoot` is everything that ties the API to the React app: the SPA proxy runs `npm start` there during development, and `dotnet publish` runs `npm run build` there and copies the result into `wwwroot`. Pointing it at `..\CoolApp.Client\` is enough for both. `Properties/launchSettings.json` is relative to the `.csproj` and moved together with it.
- The solution only needs its path to the project updated. `dotnet sln remove`/`add` does that.
- Update anything else that names paths: `.github/CODEOWNERS` (`/Controllers/` becomes `/CoolApp.Api/Controllers/`, `/ClientApp/` becomes `/CoolApp.Client/`), the README's project map, `CONTRIBUTING.md`, and any CI scripts.

`dotnet build` from the root builds the solution as before. To run the app, name the project: `dotnet run --project CoolApp.Api`, or `cd CoolApp.Api` and then `dotnet run`. It still starts the React development server for you. For frontend commands, `cd CoolApp.Client`. Check that the app still starts and `CoolApp.Api/CoolApp.http` still works before you go on.

## 2. Add a Test Project

The test project gets its own folder next to the API. The name says what it tests (`CoolApp.Api`) and with which framework (`XUnit`), because the same tests also exist in NUnit and MSTest for comparison (see [The Same Tests in NUnit and MSTest](#the-same-tests-in-nunit-and-mstest) at the end). Create `CoolApp.Api.Tests.XUnit/CoolApp.Api.Tests.XUnit.csproj`:

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
    <ProjectReference Include="..\CoolApp.Api\CoolApp.Api.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.14" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="18.12.0" />
    <PackageReference Include="NSubstitute" Version="6.2.0" />
    <PackageReference Include="xunit.v3" Version="4.0.2" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

</Project>
```

(`dotnet new install xunit.v3.templates` and `dotnet new xunit3 -o CoolApp.Api.Tests.XUnit -f net10.0` creates a similar project, but the template targets an older test runner setup. Writing these lines yourself is simpler and matches the theory demo.)

- `xunit.v3` is the test framework. A v3 test project is an executable, which is why `OutputType` is `Exe`.
- `Microsoft.AspNetCore.Mvc.Testing` provides `WebApplicationFactory`, which starts your real app in memory for integration tests.
- `NSubstitute` generates test doubles from an interface. The controller tests (step 6) use it, the same library as the theory demo's mock example.
- `Microsoft.Testing.Extensions.CodeCoverage` collects coverage (step 10).
- The `ProjectReference` (`..\CoolApp.Api\CoolApp.Api.csproj`) points at the app, so the tests can use its classes.

Two more changes:

1. **Add the project to the solution**, so `dotnet build`, `dotnet test` and your IDE all see it:

   ```pwsh
   dotnet sln add CoolApp.Api.Tests.XUnit/CoolApp.Api.Tests.XUnit.csproj
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

Add `CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

Lecture 06 ended with a warning: the service only works with a repository that returns copies because it calls `repository.Update(...)` after every change. Turn that warning into a test. Add the fake `CoolApp.Api.Tests.XUnit/Unit/CopyingStudySessionRepository.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

and `CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceContractTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

Add `CoolApp.Api.Tests.XUnit/Unit/RatingTests.cs`. These are small, but they pin down the 1-5 rule from Lecture 04 at its edges:

```csharp
using CoolApp.Models;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

## 6. Controller Tests with a Mocked Service

The service tests above never touch the controller, and the integration tests in the next step go through all of it at once. In between, test the **controller on its own**: create it with `new`, give it a test double instead of the real service, call its methods directly and check what they return. Add `CoolApp.Api.Tests.XUnit/Unit/StudySessionsControllerTests.cs`:

```csharp
using CoolApp.Controllers;
using CoolApp.Models;
using CoolApp.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoolApp.Api.Tests.XUnit.Unit;

// Tests StudySessionsController on its own: the controller is created with `new`, its
// methods are called directly, and the return values are checked. No web server, no
// routing, no JSON - that's what the integration tests are for.
//
// The service is a test double generated by NSubstitute. Used as a STUB, it returns
// whatever the test tells it to, so each test picks the case it needs (found, not found)
// without any real data. Used as a MOCK, the test checks how the controller called it.
public class StudySessionsControllerTests
{
    private readonly IStudySessionService _service = Substitute.For<IStudySessionService>();
    private readonly StudySessionsController _controller;

    public StudySessionsControllerTests()
    {
        _controller = new StudySessionsController(_service);
    }

    private static StudySession Session(int id) => new()
    {
        Id = id,
        Course = "Software Development I",
        Topic = "Controller tests",
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };

    [Fact]
    public void GetById_ServiceFindsTheSession_ReturnsIt()
    {
        var session = Session(7);
        _service.GetById(7).Returns(session);

        var result = _controller.GetById(7);

        Assert.Same(session, result.Value);
    }

    [Fact]
    public void GetById_ServiceReturnsNull_Returns404()
    {
        _service.GetById(7).Returns((StudySession?)null);

        var result = _controller.GetById(7);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public void Create_ReturnsCreatedAtGetByIdWithTheNewId()
    {
        var created = Session(42);
        _service.Create(Arg.Any<StudySession>()).Returns(created);

        var result = _controller.Create(Session(0));

        var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(StudySessionsController.GetById), createdAt.ActionName);
        Assert.Equal(42, createdAt.RouteValues!["id"]);
        Assert.Same(created, createdAt.Value);
    }

    [Theory]
    [InlineData(true, typeof(NoContentResult))]
    [InlineData(false, typeof(NotFoundResult))]
    public void RateSession_ServiceResult_MapsToStatusCode(bool rated, Type expected)
    {
        _service.Rate(7, 5).Returns(rated);

        var result = _controller.RateSession(7, new RatingSubmission(5));

        Assert.IsType(expected, result);
    }

    // A mock: checks that the query parameters reach the service unchanged. Use this kind
    // of check sparingly - it tests how the controller works, not what it returns.
    [Fact]
    public void GetAll_PassesTheFiltersToTheService()
    {
        _controller.GetAll(course: "PSI", minSeatsAvailable: 2);

        _service.Received(1).GetUpcoming("PSI", 2, null);
    }
}
```

- `Substitute.For<IStudySessionService>()` generates an implementation of the interface. Nothing in it does anything until a test says so. This only works because the controller depends on the **interface** (Lecture 06's Dependency Inversion), so a test can hand it something else.
- As a **stub**, `.Returns(...)` decides the answer for the case the test needs. `GetById_ServiceReturnsNull_Returns404` needs a missing session, and that's one line instead of finding an id that doesn't exist.
- The tests check the **return values** the way MVC would read them. `ActionResult<T>.Value` holds the session when the action returned one, `ActionResult<T>.Result` holds a `NotFoundResult` when it returned `NotFound()`, and `CreatedAtActionResult` carries the action name and the route values that become the `Location` header.
- As a **mock**, `Received(1).GetUpcoming(...)` checks how the controller *called* the service. The theory lecture's warning applies: this tests how the code works, not what it does, so keep it to the few things that matter (here: that the filters reach the service).
- What these tests can't see: routing (`[HttpGet("{id:int}")]`), model binding and `[ApiController]` validation only happen in the real pipeline. Calling `_controller.Create(...)` with an invalid session would not return 400. That's the next step's job.

Try it: in `RateSession`, change `: NotFound()` to `: NoContent()`. Only `RateSession_ServiceResult_MapsToStatusCode(rated: False, ...)` fails, and none of the integration tests notice, because none of them rates a missing session. Put it back.

## 7. Integration Tests Through the Real API

Unit tests, including the controller tests, can't see routing, model validation, JSON serialization or the DI registrations in `Program.cs`. Integration tests can. Add `CoolApp.Api.Tests.XUnit/Integration/StudySessionsApiTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CoolApp.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoolApp.Api.Tests.XUnit.Integration;

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

## 8. Frontend Tests with Jest and React Testing Library

The React app has its own test runner already: the ASP.NET Core + React template is built on Create React App, which includes **Jest**. The template's `App.test.js` ("renders without crashing") has been there since Lecture 01. Add **React Testing Library**, which renders a component and lets a test use it the way a user would:

```pwsh
cd CoolApp.Client
npm install --save-dev --save-exact @testing-library/react@16.3.3 @testing-library/dom@10.4.2
```

Like the .NET tests, the frontend tests get their own folder: **`CoolApp.Client/tests/`, next to `src/`** instead of inside it. `src/` is what gets built and shipped, and the tests stay out of it. Create React App only looks for tests in `src/` by default, so tell it where they are. In `CoolApp.Client/package.json`, change the `test` script and add a `test:coverage` script:

```json
"test": "cross-env CI=true react-scripts test --env=jsdom --roots=src --roots=tests",
"test:coverage": "npm test -- --coverage"
```

and add a `jest` section next to `scripts`:

```json
"jest": {
  "testMatch": [
    "<rootDir>/tests/**/*.test.js"
  ],
  "collectCoverageFrom": [
    "src/**/*.js",
    "!src/index.js",
    "!src/reportWebVitals.js",
    "!src/service-worker.js",
    "!src/serviceWorkerRegistration.js",
    "!src/setupProxy.js"
  ],
  "coverageReporters": [
    "text-summary",
    "cobertura"
  ]
},
```

- `--roots=src --roots=tests` makes Jest look at both folders: `tests/` for the tests, `src/` for the code they import (and for coverage). Create React App lets `package.json` override only some Jest options. `testMatch` and the coverage options are among them, but `roots` isn't, so it goes on the command line.
- `testMatch` runs only `*.test.js` files under `tests/`.
- `collectCoverageFrom` and `coverageReporters` are for coverage in step 10. The excluded files are template plumbing (the entry point, the service worker, the dev proxy) that nobody tests. It's the same idea as excluding generated C# code.

Move the template's test there too, and fix its import:

```pwsh
mkdir tests
git mv src/App.test.js tests/App.test.js
```

In `tests/App.test.js`, `import App from './App';` becomes `import App from '../src/App';`.

Then add `CoolApp.Client/tests/StudySessions.test.js`:

```js
import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { StudySessions } from '../src/components/StudySessions';

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

Run them from `CoolApp.Client/`:

```pwsh
npm test
```

All 6 tests pass: 5 here plus the template's `App.test.js`. Then go back to the repository root (`cd ..`).

These tests check the React code on its own. They can't tell whether the frontend and the real API still agree on the JSON. That's what **end-to-end** tests do: the theory lecture's Playwright tests drive a real browser against the running app. They come in a later lecture.

## 9. Run the Tests

```pwsh
dotnet test
```

All 26 .NET tests should pass in a few seconds (`dotnet test` runs only the .NET tests, and `npm test` runs the frontend ones). Then **break something on purpose** and run them again. In `StudySession.CompareTo`, swap the `-1` and the `1`: exactly one test fails, `GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime`, and its name tells you what broke. Undo that, and in `StudySessionExtensions.Filter` change `>= minSeatsAvailable` to `> minSeatsAvailable`. This time **nothing fails**, because no test uses the seats filter. That's a real gap, and the coverage report in the next step shows it as red lines. Undo it, and add a `[Theory]` for the seats filter to your own project's tests.

## 10. Measure Coverage

Install ReportGenerator as a local tool. If you already have `dotnet-tools.json` from an earlier lecture, skip `dotnet new tool-manifest`:

```pwsh
dotnet new tool-manifest
dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.12
```

Collect coverage while the .NET tests run, then while the frontend tests run, and turn both into one HTML report:

```pwsh
dotnet test --project CoolApp.Api.Tests.XUnit --coverage --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
cd CoolApp.Client
npm run test:coverage
cd ..
dotnet reportgenerator "-reports:TestResults/coverage.cobertura.xml;CoolApp.Client/coverage/cobertura-coverage.xml" -targetdir:coverage-report "-classfilters:-Microsoft.AspNetCore.OpenApi.Generated*;-System.Runtime.CompilerServices*"
```

Both tools write the same **Cobertura** XML format, so ReportGenerator reads them together (the `;` separates the files). `npm run test:coverage` also prints a short summary, and its file lands in `CoolApp.Client/coverage/`, which the React app's own `.gitignore` already ignores.

Open `coverage-report/index.html`. It lists the C# code as `CoolApp.Api` at about **80% line coverage**, and the React code as `src` and `src.components`, about **63%** together. All together it's about **71%**. Run the last command again without the `-classfilters` part and the C# number drops to about **26%**. The difference is code generated by the compiler and the OpenAPI package, which nobody writes or tests. Excluding generated code, as the theory lecture recommends, is what makes the number mean something. The `collectCoverageFrom` exclusions from step 8 do the same for the React template's plumbing. Click into a class or file to see exactly which lines ran: `StudySessionSummary`, the error page and `SessionSummaries.js` are still at 0%.

Coverage only shows that a line *ran*, not that a test *checked* its result. A test without a single `Assert` raises coverage too. Use the number to find untested code, not to prove the code is correct.

The report is generated output, so add it to `.gitignore` (`TestResults/` is already ignored by the standard Visual Studio `.gitignore`):

```gitignore
# Coverage reports (Lecture 08) - generated by `dotnet reportgenerator`, never committed
coverage-report/
```

## 11. Checkpoint

```pwsh
dotnet build
dotnet test
cd CoolApp.Client
npm test
cd ..
```

The app builds and all tests pass (26 .NET, 6 frontend). Apart from its new location, nothing about the app itself changed in this stage, so `dotnet run --project CoolApp.Api` and `CoolApp.http` behave exactly as before.

# Stage 2 - async/await and Cancellation

Today's repository keeps sessions in a list, but a real one talks to a database, a file or another service: I/O. Synchronous code **blocks** a thread while it waits for I/O. With `async`/`await`, the thread goes back to the pool and serves other requests, and the method continues when the answer arrives. Making the repository interface async now means a database-backed implementation can be swapped in later without touching any caller.

`async` spreads upward: once the repository is async, the service has to `await` it, so the service is async, and so is the controller. Go from the bottom up. **The build stays red from step 12 until step 16**, because each layer still calls the old methods of the layer below. That's normal for this refactor. The tests come back green at step 17, which is why they were written first.

## 12. Make the Repository Interface Async

Replace `CoolApp.Api/Repositories/IStudySessionRepository.cs`:

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

## 13. Update the In-Memory Repository

Replace `CoolApp.Api/Repositories/InMemoryStudySessionRepository.cs`:

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

## 14. Make the Service Async

Replace `CoolApp.Api/Services/IStudySessionService.cs`:

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

and `CoolApp.Api/Services/StudySessionService.cs`:

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

## 15. Make the Controller Async

Replace `CoolApp.Api/Controllers/StudySessionsController.cs`:

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

## 16. Update the Tests, and Test Cancellation

`dotnet build` now succeeds for the app, but the test project still calls the old synchronous methods. Replace `CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

`CoolApp.Api.Tests.XUnit/Unit/CopyingStudySessionRepository.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Repositories;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

`CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceContractTests.cs`:

```csharp
using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Api.Tests.XUnit.Unit;

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

and `CoolApp.Api.Tests.XUnit/Unit/StudySessionsControllerTests.cs`:

```csharp
using CoolApp.Controllers;
using CoolApp.Models;
using CoolApp.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoolApp.Api.Tests.XUnit.Unit;

// Tests StudySessionsController on its own: the controller is created with `new`, its
// methods are called directly, and the return values are checked. No web server, no
// routing, no JSON - that's what the integration tests are for.
//
// The service is a test double generated by NSubstitute. Used as a STUB, it returns
// whatever the test tells it to, so each test picks the case it needs (found, not found)
// without any real data. Used as a MOCK, the test checks how the controller called it.
public class StudySessionsControllerTests
{
    private readonly IStudySessionService _service = Substitute.For<IStudySessionService>();
    private readonly StudySessionsController _controller;

    public StudySessionsControllerTests()
    {
        _controller = new StudySessionsController(_service);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static StudySession Session(int id) => new()
    {
        Id = id,
        Course = "Software Development I",
        Topic = "Controller tests",
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };

    [Fact]
    public async Task GetById_ServiceFindsTheSession_ReturnsIt()
    {
        var session = Session(7);
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _controller.GetById(7, Cancellation);

        Assert.Same(session, result.Value);
    }

    [Fact]
    public async Task GetById_ServiceReturnsNull_Returns404()
    {
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns((StudySession?)null);

        var result = await _controller.GetById(7, Cancellation);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Create_ReturnsCreatedAtGetByIdWithTheNewId()
    {
        var created = Session(42);
        _service.CreateAsync(Arg.Any<StudySession>(), Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.Create(Session(0), Cancellation);

        var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(nameof(StudySessionsController.GetById), createdAt.ActionName);
        Assert.Equal(42, createdAt.RouteValues!["id"]);
        Assert.Same(created, createdAt.Value);
    }

    [Theory]
    [InlineData(true, typeof(NoContentResult))]
    [InlineData(false, typeof(NotFoundResult))]
    public async Task RateSession_ServiceResult_MapsToStatusCode(bool rated, Type expected)
    {
        _service.RateAsync(7, 5, Arg.Any<CancellationToken>()).Returns(rated);

        var result = await _controller.RateSession(7, new RatingSubmission(5), Cancellation);

        Assert.IsType(expected, result);
    }

    // A mock: checks that the query parameters - and the request's CancellationToken -
    // reach the service unchanged. A token the controller accepts but doesn't pass on
    // cancels nothing, and only a check like this one notices. Use this kind of check
    // sparingly: it tests how the controller works, not what it returns.
    [Fact]
    public async Task GetAll_PassesTheFiltersAndTheTokenToTheService()
    {
        using var request = new CancellationTokenSource();

        await _controller.GetAll(request.Token, course: "PSI", minSeatsAvailable: 2);

        await _service.Received(1).GetUpcomingAsync("PSI", 2, null, request.Token);
    }
}
```

The changes are mechanical: test methods become `async Task`, calls are awaited, and every call passes `TestContext.Current.CancellationToken`. The new test at the end creates a `CancellationTokenSource`, cancels it **before** calling the service, and asserts that the call throws instead of returning sessions. That's the contract from step 12, checked through the service, the way a disconnected client's request would reach it. `ThrowsAnyAsync` accepts `OperationCanceledException` and its subclasses, such as `TaskCanceledException`, which a real database driver may throw instead. In the controller tests, the stubs are set up with `Arg.Any<CancellationToken>()` because the token doesn't matter for what they check. The mock test now passes a token of its own and checks that the **same** token reaches the service. Try it: in `GetAll`, pass `CancellationToken.None` to the service instead of `cancellationToken`. Everything still compiles and every other test passes, but this one fails. The integration tests and `RatingTests` need no changes: they already used the HTTP API asynchronously.

## 17. Checkpoint: Build and Test

```pwsh
dotnet build
dotnet test
```

All 27 tests pass: the same 26 as before the refactor, plus the cancellation test. The 6 frontend tests are unaffected (`npm test` in `CoolApp.Client/`), since the API didn't change.

## 18. Run the App Once More

The tests are the safety net, but look at the real thing once too. Run `dotnet run --project CoolApp.Api` and go through `CoolApp.Api/CoolApp.http`. Nothing should behave differently: `GET`s, `POST` (201, and a `Location` header pointing at the new id), invalid `POST` (400), `PUT`, ratings, `DELETE`, and the filters.

## The Same Tests in NUnit and MSTest

xUnit is one of three common .NET test frameworks. **NUnit** and **MSTest** are the other two, and you'll meet all three in real projects. The repository has the stage 2 tests written in each of them, one to one, so you can compare them file by file:

- `CoolApp.Api.Tests.NUnit/` (NUnit 4)
- `CoolApp.Api.Tests.MSTest/` (MSTest 4)

They aren't in `CoolApp.sln`, so `dotnet test` from the root still runs only the xUnit tests. Run the others by name:

```pwsh
dotnet test --project CoolApp.Api.Tests.NUnit
dotnet test --project CoolApp.Api.Tests.MSTest
```

Each runs the same 27 tests. The ideas are the same, and so are the test doubles (NSubstitute doesn't care which framework runs it). What changes:

| | xUnit | NUnit | MSTest |
| --- | --- | --- | --- |
| A test | `[Fact]` | `[Test]` | `[TestMethod]` (class needs `[TestClass]`) |
| Test cases with data | `[Theory]` + `[InlineData(...)]` | `[TestCase(...)]` | `[TestMethod]` + `[DataRow(...)]` |
| Equality | `Assert.Equal(expected, actual)` | `Assert.That(actual, Is.EqualTo(expected))` | `Assert.AreEqual(expected, actual)` |
| "Throws this or a subclass" | `Assert.ThrowsAnyAsync<T>` | `Assert.CatchAsync<T>` | `Assert.ThrowsAsync<T>` |
| Per-test setup | constructor | `[SetUp]` method | constructor (or `[TestInitialize]`) |
| One instance of the class... | per test | **for all tests in the class** | per test |
| Shared, expensive setup | `IClassFixture<T>` | `[OneTimeSetUp]` / `[OneTimeTearDown]` | static `[ClassInitialize]` / `[ClassCleanup]` |
| The test's cancellation token | `TestContext.Current.CancellationToken` | `TestContext.CurrentContext.CancellationToken` | `TestContext.CancellationToken` (from the constructor) |

The row to remember is the instance one. xUnit and MSTest create a new instance of the test class for every test, so a field like `_repository = new()` is fresh for each test. NUnit runs all the tests of a class on **one** instance, so the same field would carry sessions from one test into the next. That's why the NUnit version creates the repository in a `[SetUp]` method. Pick whichever framework your team prefers. If your project already uses NUnit or MSTest, keep it.

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

The database part is next: [WALKTHROUGH-09.md](WALKTHROUGH-09.md). See [WALKTHROUGHS.md](WALKTHROUGHS.md) for every walkthrough.
