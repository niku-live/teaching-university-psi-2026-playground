# Walkthrough 09: A Real Database with Entity Framework Core, then Concurrency Conflicts and a Custom Exception

This is the step-by-step walkthrough behind [Lecture 09](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture09): storing StudySpot's sessions in a **SQLite** database with **Entity Framework Core**, running the integration tests against a real database, and then making sure two people editing the same session can't silently overwrite each other - with **our own exception type** and **one exception handler for the whole API**. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-08.md](WALKTHROUGH-08.md): the API in `CoolApp.Api/`, an async `IStudySessionRepository` with a required `CancellationToken`, an in-memory implementation of it, and an xUnit test project with unit and `WebApplicationFactory` integration tests. Apply these same steps to **your own team's project**, not just this repository. It follows the [Persistence and ORM theory lecture](https://github.com/smagurauskas/software-engineering/blob/main/08-persistence-and-orm.qmd) (`DbContext`, migrations with the `dotnet ef` tool, seeding, async queries, concurrency tokens). It covers two Lab Assignment #2 requirements: "relational database and Entity Framework is used for storing all the data" and "create at least 1 exception type and throw it; meaningfully deal with it by attaching custom handling logic". It also prepares a Lab Assignment #3 one: "Entity Framework migrations are used and run automatically".

The work happens in **two stages, and the project builds and all tests pass at the end of each one**:

1. **Stage 1 - Entity Framework Core with SQLite (steps 1-8):** a `DbContext`, a second implementation of `IStudySessionRepository`, the first migration, and integration tests that run against a real database.
2. **Stage 2 - Concurrency conflicts, a custom exception and a global handler (steps 9-16):** a version on every session, a `StudySessionConflictException` instead of a lost update, and an `IExceptionHandler` that turns it into a `409 Conflict` for every endpoint.

## 0. Why a Database Now, and Why SQLite

Until now every session lived in a `List` inside a singleton: restart the app and everything added since is gone, and two requests that run at the same time share one `List<T>`, which isn't safe for concurrent writes. A database fixes both. Thanks to Lecture 06 and Lecture 08 this is a small change: everything talks to `IStudySessionRepository`, which is already async, so the database is **one more implementation of that interface**, swapped in with one line in `Program.cs`. The tests from Lecture 08 then show that nothing else changed.

**SQLite** is a real relational database that runs inside the application and keeps everything in one file. There's no server to install or start, which makes it the quickest way to get going. The theory lecture's demo uses **PostgreSQL** in Docker instead, which is closer to what a production app would use. Your team can pick either one (or SQL Server, or MySQL): the steps are the same, and only the provider package, the `Use...` call, the connection string and a few details change. The "If You Use PostgreSQL" section at the end lists them.

# Stage 1 - Entity Framework Core with SQLite

## 1. Add the Packages and the `dotnet ef` Tool

In `CoolApp.Api/CoolApp.Api.csproj`, next to the other `PackageReference`s:

```xml
    <!-- Entity Framework Core with SQLite: the database is a single file, no server to install -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.14" />
    <!-- Needed only by the `dotnet ef` tool, not at run time -->
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.14">
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
```

From the repository root, add `dotnet ef` to the local tool manifest from Lecture 08 (`dotnet-tools.json`):

```pwsh
dotnet tool install dotnet-ef --version 10.0.14
```

- `Microsoft.EntityFrameworkCore.Sqlite` is the EF Core provider for SQLite. It brings EF Core itself along.
- `Microsoft.EntityFrameworkCore.Design` is only used by `dotnet ef` while you create migrations. `PrivateAssets` keeps it out of anything that references the project.
- Keep the EF packages and the tool on the same version. A teammate who pulls your branch runs `dotnet tool restore` once and gets the same `dotnet ef`.

## 2. Add the DbContext

Add `CoolApp.Api/Data/StudySpotDbContext.cs`:

```csharp
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The Entity Framework "unit of work": one instance per request (AddDbContext registers
// it as scoped), tracking the entities it loaded and turning SaveChanges into SQL.
//
// Only the repository uses it. The service and the controller never see it, so the
// rest of the app doesn't know (or care) that sessions now live in a database.
public class StudySpotDbContext(DbContextOptions<StudySpotDbContext> options) : DbContext(options)
{
    public DbSet<StudySession> StudySessions => Set<StudySession>();

    // Only what conventions cannot guess has to be configured. Id becomes the primary
    // key, [Required] becomes NOT NULL (SQLite ignores the [StringLength] lengths, but
    // a real server database would use them for the column types), and
    // Status isn't mapped at all: it only has a getter, so it's calculated every time.
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var session = modelBuilder.Entity<StudySession>();

        // Rating is our own struct, not a type a database column can hold - store its
        // int Value, and go back through the Rating constructor (and its 1-5 check) when
        // reading. "Not rated yet" stays NULL.
        session.Property(s => s.HostRating)
            .HasConversion(rating => rating!.Value.Value, value => new Rating(value));
    }

    // The same configuration for the application and the `dotnet ef` tool.
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseSqlite(connectionString)
            // Runs after migrations are applied (by Migrate or `dotnet ef database update`),
            // every time - so it has to check whether the data is already there.
            .UseSeeding((context, _) =>
            {
                if (!context.Set<StudySession>().Any())
                {
                    context.Set<StudySession>().AddRange(SampleSessions());
                    context.SaveChanges();
                }
            })
            .UseAsyncSeeding(async (context, _, cancellationToken) =>
            {
                if (!await context.Set<StudySession>().AnyAsync(cancellationToken))
                {
                    context.Set<StudySession>().AddRange(SampleSessions());
                    await context.SaveChangesAsync(cancellationToken);
                }
            });

    // The same two sessions InMemoryStudySessionRepository starts with. Not HasData (part
    // of a migration), because they start "tomorrow" relative to the first run.
    private static IEnumerable<StudySession> SampleSessions() =>
    [
        new StudySession
        {
            Course = "Software Development I",
            Topic = "Pull request etiquette",
            Location = "MIF, room 401",
            StartsAt = DateTimeOffset.UtcNow.AddDays(1),
            HostName = "Ieva",
            SeatsAvailable = 3
        },
        new StudySession
        {
            Course = "Software Development I",
            Topic = ".NET memory model (stack vs heap)",
            Location = "Library, 2nd floor",
            StartsAt = DateTimeOffset.UtcNow.AddDays(2),
            HostName = "Tomas",
            SeatsAvailable = 5
        }
    ];
}
```

- A `DbSet<StudySession>` is the table. Queries against it (`db.StudySessions.Where(...)`) are translated to SQL.
- **Conventions** do most of the mapping: `Id` becomes the primary key, generated by the database, `[Required]` from Lecture 03 becomes `NOT NULL`, and `Status` has only a getter, so it isn't stored at all.
- **`Rating` needs a value converter.** It's our own struct from Lecture 04, which no database column type knows. Storing `Value` and reading it back through the constructor keeps the 1-5 check working for data that comes from the database too.
- `Configure` holds the configuration in one place, as in the theory demo. **`UseSeeding`/`UseAsyncSeeding`** add the two sample sessions after the migrations are applied, if the table is empty. EF calls the sync one from `dotnet ef database update` and the async one from `MigrateAsync`, so both are needed.

## 3. Add the Entity Framework Repository

Add `CoolApp.Api/Repositories/EfStudySessionRepository.cs`:

```csharp
using CoolApp.Data;
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Repositories;

// The Entity Framework implementation of the same IStudySessionRepository contract the
// in-memory one honors - which is why nothing outside Program.cs had to change.
//
// Scoped in Program.cs, because the DbContext it depends on is scoped.
//
// Every query uses EF's async version (ToListAsync, SaveChangesAsync, ...) and passes the
// CancellationToken on: if the client gives up on the request, the database query is
// cancelled too instead of running to completion for nobody.
public class EfStudySessionRepository(StudySpotDbContext db) : IStudySessionRepository
{
    // AsNoTracking: hand out plain copies that the DbContext forgets about. Changing one
    // changes nothing in the database - exactly the "may return a copy" case the
    // interface's contract warned about in Lecture 06. The only way to persist a change
    // is Update.
    public async Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken) =>
        await db.StudySessions.AsNoTracking().ToListAsync(cancellationToken);

    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        db.StudySessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken)
    {
        // The contract says Add assigns the Id. Reset whatever the client sent, so the
        // database generates it instead of trying to insert a duplicate key.
        session.Id = 0;
        db.StudySessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
        return session;
    }

    public async Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken)
    {
        if (!await db.StudySessions.AnyAsync(s => s.Id == session.Id, cancellationToken))
        {
            return false;
        }

        // Update attaches the (untracked) session and marks every column as changed.
        db.StudySessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ExecuteDeleteAsync runs a single DELETE statement - no need to load the session first.
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.StudySessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
```

- It implements the same interface as `InMemoryStudySessionRepository`, with the same contract from Lecture 06: `GetByIdAsync` returns `null` for a missing id, `UpdateAsync`/`DeleteAsync` return `false`.
- **Everything is async and gets the token.** `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync` and `ExecuteDeleteAsync` all take the `CancellationToken`, so a request the client gave up on stops the database query too. That's the "no IO is performed synchronously" requirement, now with real IO.
- **`AsNoTracking`** returns copies the `DbContext` doesn't watch. This is exactly the "may return a copy" case from the Lecture 06 contract, and the reason `StudySessionService` always calls `UpdateAsync` instead of relying on changing the object it got back. The `CopyingStudySessionRepository` fake from Lecture 08 has been guarding that all along.
- `ExecuteDeleteAsync` sends one `DELETE ... WHERE "Id" = @id` without loading the session first.

Two small comment updates, so the code says what's true now. In `IStudySessionRepository.cs`:

```csharp
// *what* they need stored and retrieved, never *where* it lives. The app uses
// EfStudySessionRepository (SQLite through Entity Framework); the unit tests use
// InMemoryStudySessionRepository. Swapping one for the other touched nothing that uses
// this interface.
```

In `InMemoryStudySessionRepository.cs`, the class stays (the unit tests use it), but its comment changes:

```csharp
// In-memory sample data. The app itself now uses EfStudySessionRepository; this one stays
// for the unit tests, which need a fast, real repository with no database behind it.
...
// If you register it in Program.cs, make it a singleton: the data lives in this
// instance's list, so a new instance per request would forget every session.
```

## 4. Register It in `Program.cs`

Add the connection string to `CoolApp.Api/appsettings.json`, before `"AllowedHosts"`:

```json
"ConnectionStrings": {
  "StudySpot": "Data Source=studyspot.db"
},
```

The database file is created next to the project the first time the app runs. It's your local data, not code, so add it to `.gitignore` (the `*` also covers the `-wal` and `-shm` files SQLite keeps next to it):

```gitignore
# The local SQLite database (Lecture 09) - created by the app on first run, never committed
studyspot.db*
```

In `CoolApp.Api/Program.cs`, add `using CoolApp.Data;` and `using Microsoft.EntityFrameworkCore;` at the top. Then replace the repository registration (the comment, the `AddSingleton` line and the `AddScoped` line after it) and add the migration block right after `var app = builder.Build();`:

```csharp
// service asks for IStudySessionRepository - neither one creates what it depends on.
//
// The connection string comes from configuration (appsettings.json locally), never from
// the code. It's read inside the lambda, when the first DbContext is created, so the
// integration tests can point the app at their own database.
//
// AddDbContext registers the DbContext as scoped (one per request), so the repository
// that depends on it is scoped too - a singleton repository would hold on to one
// DbContext forever (a "captive dependency"). Swapping InMemoryStudySessionRepository
// for EfStudySessionRepository is the only change the rest of the app needs.
builder.Services.AddDbContext<StudySpotDbContext>((services, options) =>
    StudySpotDbContext.Configure(options,
        services.GetRequiredService<IConfiguration>().GetConnectionString("StudySpot")
            ?? throw new InvalidOperationException("Connection string 'StudySpot' is missing")));
builder.Services.AddScoped<IStudySessionRepository, EfStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();

var app = builder.Build();

// Create or upgrade the database to the latest migration (UseSeeding then adds the sample
// sessions). Convenient during development only: in production, several instances
// starting at once would all try to migrate. A DbContext is scoped, so it has to come
// from a scope - there is no request (and so no request scope) while the app starts.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<StudySpotDbContext>().Database.MigrateAsync();
}
```

- **This is the swap.** `AddSingleton<IStudySessionRepository, InMemoryStudySessionRepository>()` becomes `AddScoped<IStudySessionRepository, EfStudySessionRepository>()`. The service and the controller don't change.
- **Scoped, not singleton.** `AddDbContext` registers the `DbContext` as scoped: one per request, disposed when the request ends. A singleton repository would keep the first request's `DbContext` forever (a "captive dependency"), and ASP.NET Core refuses to start in Development if you try.
- The connection string is read **inside the lambda**, when the first `DbContext` is created, rather than while `Program.cs` builds the services. That lets the integration tests (step 7) replace it with their own database.
- **Migrations run on startup only in Development**, as the theory lecture recommends. `MigrateAsync` creates the database and its tables, or upgrades them to the latest migration. In production, several copies of the app starting at once would all try to migrate at the same time. There, use `dotnet ef migrations script --idempotent` or `dotnet ef database update` from the deployment pipeline instead.

## 5. Create the First Migration

From the repository root:

```pwsh
dotnet ef migrations add InitialCreate --project CoolApp.Api
```

This adds `CoolApp.Api/Migrations/`, with three files: `<timestamp>_InitialCreate.cs` (what to change), its `.Designer.cs` and `StudySpotDbContextModelSnapshot.cs` (the whole model as of this migration, which the next migration is compared against). Read the `Up` method before you commit it:

```csharp
            migrationBuilder.CreateTable(
                name: "StudySessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Course = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    StartsAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    HostName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    SeatsAvailable = table.Column<int>(type: "INTEGER", nullable: false),
                    HostRating = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudySessions", x => x.Id);
                });
```

- **Migrations are code, so commit them and review them in pull requests.** Never edit a migration that was already applied somewhere (merged to `main`). Add a new one instead.
- `dotnet ef` doesn't touch the database here. It builds the project, starts the host from `Program.cs` far enough to get the `DbContext`, and compares the model with the snapshot.
- SQLite has only a few column types, so every string is `TEXT` and the `maxLength` from `[StringLength]` isn't enforced by the database (the API's validation still enforces it). A server database like PostgreSQL turns the same model into `character varying(100)`.
- If two teammates add migrations on two branches, they conflict in the snapshot. Merge `main` first, then `dotnet ef migrations remove` your migration and add it again on top.

## 6. Run the App, and See the SQL

```pwsh
dotnet run --project CoolApp.Api
```

The app creates `CoolApp.Api/studyspot.db`, applies the migration and adds the sample sessions. Go through `CoolApp.Api/CoolApp.http`: everything works as before, but this time **the data survives a restart** of the app. Delete the `studyspot.db` files (with the app stopped) whenever you want to start from an empty database.

**Optional: see the SQL.** Stop the app and start it with EF's command log turned on (command-line arguments override `appsettings.json`):

```pwsh
dotnet run --project CoolApp.Api -- --Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command=Information
```

Every request now logs the SQL it sent, for example `GET /api/studysessions`:

```sql
SELECT "s"."Id", "s"."Course", "s"."HostName", "s"."HostRating", "s"."Location", "s"."SeatsAvailable", "s"."StartsAt", "s"."Topic"
FROM "StudySessions" AS "s"
```

Notice that this loads **every** session, and `UpcomingOnly`/`Filter` from Lecture 04 then filter in C#. That's fine for a few hundred rows, but with a real amount of data the filter belongs in the `WHERE` (an `IQueryable` query in the repository) - see "Deferred execution" in the theory lecture. (SQLite also can't compare `DateTimeOffset` values in SQL, which is one more reason this demo keeps that filter in C#.)

To look inside the file itself, open it with any SQLite tool: the free [DB Browser for SQLite](https://sqlitebrowser.org/), the SQLite extension for VS Code, or Rider's and Visual Studio's database windows.

## 7. Run the Integration Tests Against Their Own Database

`WebApplicationFactory<Program>` starts the real `Program.cs`, which now needs a database. The tests must not use your `studyspot.db`: they would see your data, and you would see theirs. The theory lecture compares the options: the EF in-memory provider (not a relational database - Microsoft recommends against it for tests), **SQLite**, or the production database in Docker (Testcontainers). Here the app already uses SQLite, so the tests get **their own SQLite file** in the temp folder, created empty for every run.

Add `CoolApp.Api.Tests.XUnit/Integration/StudySpotApiFactory.cs`:

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

[assembly: AssemblyFixture(typeof(CoolApp.Api.Tests.XUnit.Integration.StudySpotApiFactory))]

namespace CoolApp.Api.Tests.XUnit.Integration;

// The real application (Program.cs), started in memory by WebApplicationFactory, with its
// own SQLite database file in the temp folder - so the tests never touch the studyspot.db
// you use while developing, and every run starts from an empty database.
//
// An assembly fixture: one database and one application for the whole test run, created
// before the first test that asks for it and removed after the last one. The application
// runs in the Development environment, so Program.cs applies the migrations itself.
public class StudySpotApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"studyspot-tests-{Guid.NewGuid()}.db");

    // Starts the application here, once, instead of on the first CreateClient: test
    // classes run in parallel, and two of them starting it at the same moment would
    // run the migrations twice at once - the "several instances migrating at the same
    // time" problem from theory lecture 08.
    public ValueTask InitializeAsync()
    {
        StartServer();
        return ValueTask.CompletedTask;
    }

    // Replaces the connection string from appsettings.json with the test database.
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:StudySpot", $"Data Source={_databasePath}");

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        // SQLite keeps pooled connections open, which keeps the file locked on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
```

- **One database and one app for the whole run.** `[assembly: AssemblyFixture(...)]` is xUnit v3's version of the theory demo's `DatabaseFixture`: created once, before the first test that asks for it, and disposed after the last one.
- `UseSetting("ConnectionStrings:StudySpot", ...)` replaces the connection string from `appsettings.json`.
- `StartServer()` starts the app right away. Without it, the app would start on the first `CreateClient()`, and two test classes running in parallel could start it twice at the same moment and run the migrations twice at once - which fails exactly as the theory lecture warns.
- `WebApplicationFactory` runs the app in the Development environment, so `Program.cs` applies the migrations to the new, empty database itself.

In `CoolApp.Api.Tests.XUnit/Integration/StudySessionsApiTests.cs`, the class gets the shared factory instead of its own `IClassFixture<WebApplicationFactory<Program>>`. Remove `using Microsoft.AspNetCore.Mvc.Testing;` and change the class declaration and its comment:

```csharp
// Starts the real application (Program.cs) in memory and talks to it over HTTP. These
// test what the unit tests cannot: routing, model validation, JSON serialization, the
// Dependency Injection registrations - and now a real database (see StudySpotApiFactory).
//
// One application (and one database) is shared by all tests, because starting it for
// every test would be slow. So each test creates the sessions it needs instead of
// relying on what another test left behind.
public class StudySessionsApiTests(StudySpotApiFactory factory)
{
```

Finally, a test from the theory lecture that catches a **forgotten migration**: an entity changed, but nobody ran `dotnet ef migrations add`. Add `CoolApp.Api.Tests.XUnit/Integration/MigrationsTests.cs`:

```csharp
using CoolApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoolApp.Api.Tests.XUnit.Integration;

public class MigrationsTests(StudySpotApiFactory factory)
{
    // Fails when an entity was changed but `dotnet ef migrations add` was not run, so a
    // forgotten migration is caught by the tests instead of by the first user.
    [Fact]
    public void Model_HasNoChangesWithoutAMigration()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
```

The unit tests don't change at all: they test the service with `InMemoryStudySessionRepository` and the fakes, and never needed a database.

## 8. Checkpoint

```pwsh
dotnet build
dotnet test
```

All 28 tests pass: the 27 from Lecture 08 (now 5 of them through a real database) and the migrations test.

# Stage 2 - Concurrency Conflicts, a Custom Exception and a Global Handler

## 9. The Problem: a Lost Update

Ieva and Tomas both open the same session. Ieva changes the seats to 2 and saves. A minute later Tomas, still looking at the old data, changes the location and saves. His `PUT` sends **every** field, including the old seat count, so Ieva's change is silently gone. Nobody gets an error, and nobody knows.

The theory lecture shows two fixes. For counters (add 1 to a value), let the database do the arithmetic in one `UPDATE`. For a person editing a record, use **optimistic concurrency**: every row has a version, the client sends back the version it read, and the database only updates the row if the version is still the same. If it isn't, someone else saved in between, and the right answer is an error that says so, not a silent overwrite.

## 10. Add a Version to Every Session

In `CoolApp.Api/Models/StudySession.cs`, after `HostRating`:

```csharp
    // Optimistic concurrency: [ConcurrencyCheck] makes EF add the version it read to the
    // WHERE of every UPDATE. EfStudySessionRepository raises it by one on every save, so
    // if someone else saved the session since it was read, no row matches and
    // SaveChanges throws DbUpdateConcurrencyException instead of overwriting their change.
    //
    // A client sends back the Version it read (GET) with its change (PUT).
    [ConcurrencyCheck]
    public int Version { get; set; }
```

In `EfStudySessionRepository.UpdateAsync`, raise the version right after `db.StudySessions.Update(session);` (the next step adds the rest of this method):

```csharp
        db.StudySessions.Update(session);
        session.Version++;
```

Then a migration for the new column:

```pwsh
dotnet ef migrations add AddVersion --project CoolApp.Api
```

- **`[ConcurrencyCheck]`** makes EF add the version to the `WHERE` of every `UPDATE` - with the value the entity had when it was attached, not the new one. `Update(session)` attaches the session with the version **the client sent**, and `Version++` sets the new one, so EF sends: `UPDATE "StudySessions" SET ..., "Version" = @new WHERE "Id" = @id AND "Version" = @read`. If no row matches, someone else saved in between, and `SaveChangesAsync` throws `DbUpdateConcurrencyException`.
- The migration adds a `Version INTEGER NOT NULL DEFAULT 0` column. Existing rows start at 0.
- `version` now appears in every JSON response, which is how a client gets it.
- Other databases have this built in: PostgreSQL's `xmin` column and SQL Server's `rowversion` change on every update by themselves, and the theory lecture uses `xmin` with `[Timestamp]`. SQLite has nothing like that, so the app raises the number itself.

## 11. Keep the Version the Client Read

`StudySessionService.UpdateAsync` loads the session and copies the client's fields onto it. Loading it gets **today's** version, which would always match. Copy the client's version too, after `existing.SeatsAvailable = session.SeatsAvailable;`:

```csharp
        // The version the client read, not the one just loaded: if someone saved the
        // session in between, the repository throws StudySessionConflictException
        // instead of silently overwriting their change.
        existing.Version = session.Version;
```

`RateAsync` doesn't need this: it changes only the rating, based on what it just loaded, and the version check still protects it from a save that happens in between.

## 12. Our Own Exception Type

Add `CoolApp.Api/Exceptions/StudySessionConflictException.cs`:

```csharp
namespace CoolApp.Exceptions;

// Thrown when a study session can't be saved because someone else saved it after it was
// read (see StudySession.Version). Our own type, not DbUpdateConcurrencyException: the
// service and controller don't know Entity Framework exists, and callers can catch
// exactly this case without catching every other database error with it.
public class StudySessionConflictException(int sessionId, Exception? innerException = null)
    : Exception($"Study session {sessionId} was changed by someone else after you loaded it. Load it again and reapply your change.", innerException)
{
    public int SessionId { get; } = sessionId;
}
```

Why not let `DbUpdateConcurrencyException` go up to the controller?

- **It would leak Entity Framework** into every layer above the repository - exactly what the repository interface is there to prevent. The service, the controller and a handler shouldn't need `using Microsoft.EntityFrameworkCore`.
- **It says what happened in the app's words.** Its name and message are about a study session and what to do next, and `SessionId` gives the handler the details it needs.
- **It can be caught precisely.** `catch (StudySessionConflictException)` catches this one case, not every database error.
- `innerException` keeps the original EF exception, with its stack trace, for the logs.

Add it to the contract in `IStudySessionRepository.cs`, under the line about `Update and Delete return false`:

```csharp
//   - Update throws StudySessionConflictException (and changes nothing) when the session
//     was changed since the caller read it - session.Version is the version they read.
//     An implementation that can't tell (InMemoryStudySessionRepository) never throws it.
```

## 13. Translate the Exception in the Repository

In `EfStudySessionRepository.cs`, add `using CoolApp.Exceptions;` and replace the end of `UpdateAsync`:

```csharp
        // Update attaches the (untracked) session and marks every column as changed. EF
        // remembers the Version it was attached with - the one the client read - as the
        // "original" value, and the new value is one higher:
        //   UPDATE ... SET "Version" = @new ... WHERE "Id" = @id AND "Version" = @read
        // so the row only changes if nobody saved it in between.
        db.StudySessions.Update(session);
        session.Version++;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Translate the EF exception into our own one, so nothing above the
            // repository has to know that Entity Framework exists.
            throw new StudySessionConflictException(session.Id, ex);
        }

        return true;
    }
```

Only the repository knows EF, so only the repository catches EF's exception, and it rethrows ours with the original inside.

## 14. One Handler for the Whole API

Without a handler, the exception reaches ASP.NET Core and the client gets a `500 Internal Server Error`: wrong, because nothing is broken on the server. A `try`/`catch` in the controller would work for this one action, but every action that saves a session would need the same one. An **`IExceptionHandler`** handles it once for every endpoint. Add `CoolApp.Api/ExceptionHandling/StudySessionConflictExceptionHandler.cs`:

```csharp
using CoolApp.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoolApp.ExceptionHandling;

// Turns StudySessionConflictException into a 409 Conflict, for every endpoint at once -
// no try/catch in the controller. Registered in Program.cs with AddExceptionHandler and
// called by app.UseExceptionHandler() when a request throws.
public class StudySessionConflictExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // false: "not mine" - the next handler (or the default 500) gets the exception.
        if (exception is not StudySessionConflictException conflict)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "The study session was changed by someone else",
                Detail = conflict.Message,
                Extensions = { ["sessionId"] = conflict.SessionId }
            }
        });
    }
}
```

Register it in `Program.cs` (add `using CoolApp.ExceptionHandling;`), right after the service registrations:

```csharp
// Errors as RFC 9457 "problem details" JSON. Exceptions that reach UseExceptionHandler
// (below) go through the registered IExceptionHandlers in order; one that no handler
// takes becomes a plain 500 problem.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<StudySessionConflictExceptionHandler>();
```

And add the middleware as the first line of the pipeline, right after `// Configure the HTTP request pipeline.`:

```csharp
app.UseExceptionHandler();
```

- **`409 Conflict`** is the HTTP status for "your request conflicts with the current state of the resource". The client can load the session again, show the person what changed, and let them decide.
- **Problem details** (RFC 9457) is the standard JSON shape for HTTP errors, with `type`, `title`, `status` and `detail`, plus our own `sessionId`. `AddProblemDetails()` registers the `IProblemDetailsService` that writes it, and it's the same shape ASP.NET Core already uses for the 400 validation errors from Lecture 03.
- **Returning `false`** means "not mine": the next registered handler gets the exception, and if no handler takes it, `UseExceptionHandler` returns a generic 500 problem without the stack trace. Add one handler per exception type that has a meaningful answer.
- `UseExceptionHandler()` goes first, so it wraps everything after it.

This is "meaningfully deal with it by attaching custom handling logic" from Lab Assignment #2: the exception is thrown where the problem is detected, and handled where the HTTP answer is decided. Logging an exception and rethrowing it, or catching `Exception` and returning `null`, isn't.

## 15. Test It

Add two tests to `StudySessionsApiTests.cs` (and `using Microsoft.AspNetCore.Mvc;` for `ProblemDetails`):

```csharp
    [Fact]
    public async Task Update_WithTheVersionJustRead_Returns204()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Fresh edit"), Cancellation);
        var session = await _client.GetFromJsonAsync<StudySession>(created.Headers.Location, Cancellation);

        session!.SeatsAvailable = 2;
        var response = await _client.PutAsJsonAsync(created.Headers.Location, session, Cancellation);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
```

```csharp
    // Two people load the same session; the first one saves, then the second one saves
    // with the version they read before that. The second change must not silently
    // overwrite the first one: StudySessionConflictException becomes a 409 problem.
    [Fact]
    public async Task Update_WithAVersionSomeoneElseAlreadyChanged_Returns409Problem()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Two editors"), Cancellation);
        var first = await _client.GetFromJsonAsync<StudySession>(created.Headers.Location, Cancellation);
        var second = await _client.GetFromJsonAsync<StudySession>(created.Headers.Location, Cancellation);

        first!.SeatsAvailable = 2;
        await _client.PutAsJsonAsync(created.Headers.Location, first, Cancellation);
        second!.Location = "Library, 2nd floor";
        var response = await _client.PutAsJsonAsync(created.Headers.Location, second, Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation);
        Assert.Equal("The study session was changed by someone else", problem!.Title);
    }
```

These are integration tests on purpose: the version check happens **in the database**, so only a test with a real database proves it works. Remove the `existing.Version = session.Version;` line from the service and watch the second test fail with a 204 instead of a 409.

Update the `PUT` in `CoolApp.Api/CoolApp.http` to send back the version. Visual Studio and the REST Client extension can take a value from an earlier response with `# @name`:

```http
### Read a session before editing it - note its "version"
# @name sessionToEdit
GET {{HostAddress}}/api/studysessions/1
Accept: application/json

### Update it, sending back the version you read (expect 204 No Content)
PUT {{HostAddress}}/api/studysessions/1
Content-Type: application/json

{
  "course": "Software Development I",
  "topic": "Pull request etiquette",
  "location": "MIF, room 401",
  "startsAt": "2027-09-24T18:00:00Z",
  "hostName": "Ieva",
  "seatsAvailable": 1,
  "version": {{sessionToEdit.response.body.$.version}}
}
```

Below it, the same `PUT` once more, under `### Send the same update again`: its version is now outdated, so it gets the 409.

## 16. Checkpoint: Build and Test

```pwsh
dotnet build
dotnet test
```

All 30 tests pass: the 28 from stage 1 and the two update tests. The 6 frontend tests are unaffected (`npm test` in `CoolApp.Client/`). The React app only rates sessions, and the rating endpoint doesn't need a version.

## 17. Run the App Once More

Run `dotnet run --project CoolApp.Api` and go through `CoolApp.Api/CoolApp.http`: read the session, update it (204), and send the same update again (409, with the problem details body). Then everything else, as in Lecture 08.

## The Same Tests in NUnit and MSTest

The side projects from Lecture 08 run the same 30 tests against their own SQLite file too. The assembly fixture becomes NUnit's `[SetUpFixture]` (its `[OneTimeSetUp]` runs once before every test in the namespace) and MSTest's static `[AssemblyInitialize]`/`[AssemblyCleanup]`. See `Integration/StudySpotApi.cs` in each:

```pwsh
dotnet test --project CoolApp.Api.Tests.NUnit
dotnet test --project CoolApp.Api.Tests.MSTest
```

## If You Use PostgreSQL

The theory lecture's demo uses PostgreSQL in Docker. If your team prefers that, the differences are:

- A `compose.yaml` with the `postgres:17-alpine` image (copy the one from the theory demo), started with `docker compose up -d`.
- `Npgsql.EntityFrameworkCore.PostgreSQL` instead of the SQLite package, `UseNpgsql(...)` instead of `UseSqlite(...)`, and a connection string like `Host=localhost;Port=5432;Database=studyspot;Username=postgres;Password=devpassword`. `EFCore.NamingConventions` with `UseSnakeCaseNamingConvention()` gives PostgreSQL-style `study_sessions` names.
- `timestamp with time zone` stores **UTC only**, and Npgsql refuses to write a `DateTimeOffset` with another offset. Convert it (`HasConversion(v => v.ToUniversalTime(), v => v)` on `StartsAt`), or a client sending `+02:00` gets a 500.
- The version column is built in: `[Timestamp] public uint Version` maps to PostgreSQL's `xmin`, and the repository doesn't need `Version++`.
- The integration tests start the database with the `Testcontainers.PostgreSql` package (see the theory lecture's `DatabaseFixture`). That needs Docker on every machine that runs the tests.

## Checklist for Your Own Project

- **All the data is in the database**, not only one entity. Lab Assignment #2 says "all the data". Each entity gets a `DbSet`, and relations between entities become navigation properties (see "Loading related data" in the theory lecture).
- **Every EF call is async** and gets the request's `CancellationToken`: `ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`. A `.ToList()` or `.SaveChanges()` on a request path is synchronous IO.
- **The migrations are committed** and the app applies them (`MigrateAsync` on startup in Development is enough for now; Lab Assignment #3 asks for them to run automatically).
- **The database file isn't committed**, and no connection string with a real password is either.
- **Your custom exception** is thrown where the problem is detected, carries what the handler needs, and is handled in one place with a meaningful answer (a status code, a message the user can act on) - not caught and ignored.

## Next Steps

Update `ROADMAP.md` (the Beta items for Entity Framework and the custom exception are done) and `README.md`'s "Project Map" and "Current Examples". Left for Lecture 10:

- A **generic** type with a generic method, now that there's a real reason for one (the theory lecture on generics).
- Lab Assignment #2's **shared memory** scenario, with concurrent collections or a lock: the database removed StudySpot's shared `List`, so it needs a new one, such as an in-memory cache or a counter of active requests.
- Request/response **DTOs**, so the API contract doesn't expose the entity (the theory lecture's "over-posting" section).
- **End-to-end tests** with Playwright, which run the React app in a real browser against the running API.

See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
