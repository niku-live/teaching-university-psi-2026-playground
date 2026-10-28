# Walkthrough 08: Entity Framework Core and async/await

This is the step-by-step walkthrough behind [Lecture 08](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture08): moving StudySpot's sessions out of an in-memory list into a real **SQLite database with Entity Framework Core**, and then making every layer **async**, with cancellation passed from the HTTP request all the way down to the database query. See [WALKTHROUGHS.md](WALKTHROUGHS.md) for the full list of per-lecture walkthroughs.

This assumes you already have a working project at the state described in [WALKTHROUGH-06.md](WALKTHROUGH-06.md): a controller that depends on `IStudySessionService`, a service that depends on `IStudySessionRepository`, and an in-memory repository, all wired up with Dependency Injection. (Lecture 07 was the Deadline 1 presentations and has no walkthrough of its own.) Apply these same steps to **your own team's project**, not just this repository. These cover two Lab Assignment #2 requirements: "relational database and Entity Framework is used for storing all the data" and "usage of `async`/`await`, no IO is performed synchronously".

The work happens in **two stages, and the project builds and behaves correctly at the end of each one**:

1. **Stage 1 - Entity Framework Core (steps 1-8):** add a `DbContext`, a second implementation of `IStudySessionRepository` that uses it, and a migration. Swap it in through Dependency Injection. Nothing outside `Program.cs` changes, which is the payoff Lecture 06 promised.
2. **Stage 2 - async/await and cancellation (steps 9-15):** make the repository, the service and the controller async, and pass the request's `CancellationToken` through every layer.

## 0. What's Wrong With the In-Memory List

Start the app, create a session with `POST /api/studysessions`, stop the app and start it again: the session is gone. Every restart (and every deployment) loses all the data. And with more than one server instance, each one has its own list. A database fixes both problems, and because the service only knows about `IStudySessionRepository`, the database only has to be known by one new class.

This walkthrough uses **SQLite**: the whole database is one file next to the project, there is no server to install, and it is enough for a team project. Switching to SQL Server or PostgreSQL later means changing the NuGet package and the `UseSqlite` line, not the rest of the code.

# Stage 1 - Entity Framework Core

## 1. Add the Packages and the `dotnet ef` Tool

From the project folder (the one with your `.csproj`):

```pwsh
dotnet add package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.14
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.14
dotnet new tool-manifest
dotnet tool install dotnet-ef --version 10.0.14
```

- `Microsoft.EntityFrameworkCore.Sqlite` is EF Core itself plus the SQLite database provider.
- `Microsoft.EntityFrameworkCore.Design` is only used at design time by the `dotnet ef` commands (to create migrations). It is marked `PrivateAssets="all"`, so it never ships with your app.
- `dotnet new tool-manifest` creates `dotnet-tools.json`, and `dotnet tool install` (without `--global`) records `dotnet-ef` in it. Commit that file: a teammate who clones the repository runs `dotnet tool restore` once and gets exactly the same tool version, with nothing installed globally on their machine.

Keep every `Microsoft.EntityFrameworkCore.*` package (and `dotnet-ef`) on the same version.

## 2. Add a Connection String, and Keep the Database Out of Git

In `appsettings.json`, add a `ConnectionStrings` section next to `AllowedHosts`:

```json
{
  "Logging": {
      "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information"
      }
    },
"ConnectionStrings": {
  "StudySpot": "Data Source=studyspot.db"
},
"AllowedHosts": "*"
}
```

`Data Source=studyspot.db` is a file path relative to where the app runs, so `dotnet run` creates `studyspot.db` in the project folder. The database file is local data, not source code. Add it to `.gitignore` (SQLite also creates the `-shm`/`-wal` files next to it while the app is running):

```gitignore
# Local SQLite database (Lecture 08) - created by `dotnet run`, never committed
*.db
*.db-shm
*.db-wal
```

## 3. Add the `DbContext`

Add `Data/StudySpotDbContext.cs`:

```csharp
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The Entity Framework "unit of work": one instance per request (AddDbContext registers
// it as scoped), tracking the entities it loaded and turning SaveChanges() into SQL.
//
// Only the repository uses it. The service and the controller never see it, so the
// rest of the app doesn't know (or care) that sessions now live in a database.
public class StudySpotDbContext(DbContextOptions<StudySpotDbContext> options) : DbContext(options)
{
    public DbSet<StudySession> StudySessions => Set<StudySession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Rating is our own struct, not a type a database column can hold - store its
        // int Value, and go back through the Rating constructor (and its 1-5 check) when
        // reading. EF applies a converter for Rating to the nullable Rating? property
        // too, and keeps "not rated yet" as NULL.
        //
        // Status needs no configuration: it only has a getter, so EF doesn't map it -
        // it's calculated from SeatsAvailable every time, never stored.
        modelBuilder.Entity<StudySession>()
            .Property(s => s.HostRating)
            .HasConversion(rating => rating!.Value.Value, value => new Rating(value));
    }
}
```

- `DbSet<StudySession>` becomes the `StudySessions` table. By convention, `Id` becomes its primary key, the database generates it, and the `[Required]`/`[StringLength]` attributes you added in Lecture 03 become `NOT NULL` and column lengths.
- `Rating` is our own `struct` (Lecture 04), so EF needs to be told how to store it: a **value converter** stores its `int` and rebuilds a `Rating` on the way back. Your own value types need the same treatment.
- `Status` is calculated from `SeatsAvailable`, has no setter, and is never stored. EF only maps properties that have a getter *and* a setter.

## 4. Add the Entity Framework Repository

Add `Repositories/EfStudySessionRepository.cs`, a second implementation of the interface from Lecture 06:

```csharp
using CoolApp.Data;
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Repositories;

// The Entity Framework implementation of the same IStudySessionRepository contract the
// in-memory one honors - which is why nothing outside Program.cs had to change.
//
// Scoped in Program.cs, because the DbContext it depends on is scoped.
public class EfStudySessionRepository(StudySpotDbContext db) : IStudySessionRepository
{
    // AsNoTracking: hand out plain copies that the DbContext forgets about. Changing one
    // changes nothing in the database - exactly the "may return a copy" case the
    // interface's contract warned about in Lecture 06. The only way to persist a change
    // is Update.
    public IReadOnlyList<StudySession> GetAll() => db.StudySessions.AsNoTracking().ToList();

    public StudySession? GetById(int id) =>
        db.StudySessions.AsNoTracking().FirstOrDefault(s => s.Id == id);

    public StudySession Add(StudySession session)
    {
        // The contract says Add assigns the Id. Reset whatever the client sent, so the
        // database generates it instead of trying to insert a duplicate key.
        session.Id = 0;
        db.StudySessions.Add(session);
        db.SaveChanges();
        return session;
    }

    public bool Update(StudySession session)
    {
        if (!db.StudySessions.Any(s => s.Id == session.Id))
        {
            return false;
        }

        db.StudySessions.Update(session);
        db.SaveChanges();
        return true;
    }

    // ExecuteDelete runs a single DELETE statement - no need to load the session first.
    public bool Delete(int id) => db.StudySessions.Where(s => s.Id == id).ExecuteDelete() > 0;
}
```

Read it against the contract written on `IStudySessionRepository`:

- `GetById` returns `null` when nothing matches (`FirstOrDefault`), and it returns a **copy**: `AsNoTracking()` means the `DbContext` forgets the object right away. This is exactly the case Lecture 06's Liskov step warned about: changing the returned object changes nothing in the database.
- `Update` returns `false` for a missing id and only then saves.
- `Add` assigns the `Id` (the database generates it on `SaveChanges`) and returns the stored session.
- `Delete` uses `ExecuteDelete`, which sends one `DELETE ... WHERE Id = @id` statement without loading the row first, and returns how many rows were deleted.

Leave `InMemoryStudySessionRepository` where it is. It is still a correct implementation of the same contract, so it's a useful stand-in when you don't want a database, for example in unit tests later.

## 5. Add Sample Data

The in-memory repository started with two sample sessions. Add `Data/SampleData.cs` so the database starts with the same two:

```csharp
using CoolApp.Models;

namespace CoolApp.Data;

// The same two sample sessions InMemoryStudySessionRepository starts with, written to
// the database the first time the app starts with an empty table.
//
// Not a migration (HasData): migrations need fixed values, and these sessions start
// "tomorrow" and "the day after" relative to whenever you first run the app.
public static class SampleData
{
    public static void Seed(StudySpotDbContext db)
    {
        if (db.StudySessions.Any())
        {
            return;
        }

        db.StudySessions.AddRange(
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
            });
        db.SaveChanges();
    }
}
```

EF also has `HasData` for seeding inside a migration, but migration data has to be fixed values. These sessions start "tomorrow", relative to when you first run the app, so they are added in code, and only when the table is empty.

## 6. Register Everything in `Program.cs`

Replace `Program.cs` with:

```csharp
using CoolApp.Data;
using CoolApp.Repositories;
using CoolApp.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllersWithViews()
    // Serialize enums (e.g. StudySession.Status) as their names ("Scheduled") instead
    // of the underlying number - readable in Swagger, the .http responses, and the UI.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dependency Injection: tell the container which concrete type to hand out whenever a
// constructor asks for an abstraction. The controller asks for IStudySessionService, the
// service asks for IStudySessionRepository - neither one creates what it depends on.
//
// The DbContext is scoped (one per request), so the repository that depends on it has
// to be scoped too - a singleton repository would hold on to one DbContext forever (a
// "captive dependency"), and ASP.NET Core refuses to start in Development if you try.
// Swapping InMemoryStudySessionRepository for EfStudySessionRepository is the only
// change the rest of the app needs: the service and controller don't know about it.
builder.Services.AddDbContext<StudySpotDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("StudySpot")));
builder.Services.AddScoped<IStudySessionRepository, EfStudySessionRepository>();
builder.Services.AddScoped<IStudySessionService, StudySessionService>();

var app = builder.Build();

// Create or upgrade the database to the latest migration, then add the sample sessions
// if the table is empty. A DbContext is scoped, so it has to come from a scope - there
// is no request (and so no request scope) while the app is starting.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();
    db.Database.Migrate();
    SampleData.Seed(db);
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller}/{action=Index}/{id?}");

app.MapFallbackToFile("index.html");

app.Run();
```

Three changes compared to Lecture 06:

1. `AddDbContext<StudySpotDbContext>` registers the `DbContext` as **scoped** (one per request) and tells it to use SQLite with the connection string from step 2.
2. The repository registration changes from `AddSingleton<IStudySessionRepository, InMemoryStudySessionRepository>()` to `AddScoped<IStudySessionRepository, EfStudySessionRepository>()`. This is the only line the rest of the app depends on. The service and the controller are untouched. Lecture 06's lifetime rule applies again here: a service can only depend on services that live as long as it does or longer. A singleton repository that holds a scoped `DbContext` is a **captive dependency**. Try it: keep `AddSingleton` with `EfStudySessionRepository` and run the app. In Development it refuses to start with `Cannot consume scoped service 'StudySpotDbContext' from singleton 'IStudySessionRepository'`.
3. After `builder.Build()`, the database is migrated and seeded. There is no request yet while the app starts, so the code creates a scope itself to get a `DbContext`.

## 7. Create the First Migration

A **migration** is C# code that changes the database schema from one version to the next. You generate it from your model, review it, and commit it with the code change that needed it:

```pwsh
dotnet ef migrations add InitialCreate
```

This builds the project and adds a `Migrations/` folder with three files: `<timestamp>_InitialCreate.cs` (the `Up`/`Down` methods, which create and drop the `StudySessions` table), its `.Designer.cs`, and `StudySpotDbContextModelSnapshot.cs` (EF's picture of the current model, which it compares against the next time you add a migration). Open `<timestamp>_InitialCreate.cs` and check each column's type and nullability. `HostRating` should be a nullable `INTEGER`, because of the value converter from step 3.

`db.Database.Migrate()` in `Program.cs` applies every migration the database doesn't have yet, so there is no separate "create the database" step. That covers Lab Assignment #3's "migrations are run automatically" early. You can also apply them by hand with `dotnet ef database update`.

Every time you change an entity later (a new property, a new entity), run `dotnet ef migrations add <WhatChanged>` again and commit the new migration. If you change the model and forget to do this, `Migrate()` stops the app at startup with a "pending model changes" error.

## 8. Checkpoint: Build, Run, Restart

```pwsh
dotnet build
dotnet run
```

The first run creates `studyspot.db` and seeds it. Then run the same `CoolApp.http` requests as in Lecture 06: they should all behave the same (the full list is in step 15). The difference shows when you **restart the app**: a session you created, updated or rated is still there.

Two things to try at this checkpoint:

- **Look at the SQL.** EF logs every command it runs under the `Microsoft.EntityFrameworkCore.Database.Command` category. Add `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` under `Logging:LogLevel` in `appsettings.Development.json`, and every request prints the SQL it ran in the console.
- **Liskov, for real this time.** In `StudySessionService.Update`, temporarily delete `return repository.Update(existing);` (and return `true` instead). With the in-memory repository, the update still looked like it worked. With the EF repository, the change is silently lost, because `GetById` returned a copy. Lecture 06 predicted this. Put the line back.

The code works at this point. The only thing left to improve is that every database call **blocks a thread** while it waits for the database.

# Stage 2 - async/await and Cancellation

A database call is I/O: your code sends a query and then waits for the answer. With synchronous code, the thread handling the request sits blocked during that wait. With `async`/`await`, the thread goes back to the pool and serves other requests, and the method continues when the answer arrives. Under load this is the difference between a server that keeps up and one that runs out of threads.

`async` spreads upward: once the repository is async, the service has to `await` it, so the service is async, so the controller is too. Go from the bottom up. The project will not build between steps 9 and 13, because each layer still calls the old synchronous methods of the layer below. It builds again at step 14.

## 9. Make the Repository Interface Async

Replace `Repositories/IStudySessionRepository.cs`:

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
- Each method takes a `CancellationToken`. It is a **required** parameter, not `CancellationToken cancellationToken = default`. With a default value, the compiler accepts `GetAllAsync()` without a token. That compiles and runs, and silently cancels nothing. Making it required means every caller has to decide what to pass.

## 10. Update the In-Memory Repository

Replace `Repositories/InMemoryStudySessionRepository.cs`:

```csharp
using CoolApp.Models;

namespace CoolApp.Repositories;

// In-memory sample data. Since Lecture 08 the app uses EfStudySessionRepository (a real
// database) instead; this one stays as a second implementation of the same contract -
// handy for trying the app without a database, or as a fast stand-in in tests.
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
    public Task<IReadOnlyList<StudySession>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StudySession>>(_sessions.ToList());

    // Returns the stored instance itself. That's an implementation detail, not part of
    // the contract (see IStudySessionRepository): callers still have to call Update.
    public Task<StudySession?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.FirstOrDefault(s => s.Id == id));

    public Task<StudySession> AddAsync(StudySession session, CancellationToken cancellationToken)
    {
        session.Id = _sessions.Count == 0 ? 1 : _sessions.Max(s => s.Id) + 1;
        _sessions.Add(session);
        return Task.FromResult(session);
    }

    public Task<bool> UpdateAsync(StudySession session, CancellationToken cancellationToken)
    {
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
        var existing = _sessions.FirstOrDefault(s => s.Id == id);
        return Task.FromResult(existing is not null && _sessions.Remove(existing));
    }
}
```

A list in memory has nothing to wait for, so there's nothing to `await`. `Task.FromResult` wraps a result that is already known in a completed `Task`. The interface is async because *some* implementation does I/O, not because every one does. Don't add `async` to a method that has no `await` in it: the compiler warns (CS1998) and you pay for a state machine that does nothing.

## 11. Update the Entity Framework Repository

Replace `Repositories/EfStudySessionRepository.cs`:

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

        db.StudySessions.Update(session);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ExecuteDeleteAsync runs a single DELETE statement - no need to load the session first.
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken) =>
        await db.StudySessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
```

Each EF call is swapped for its async version: `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `SaveChangesAsync`, `ExecuteDeleteAsync`. Each one gets the `cancellationToken`. `GetByIdAsync` returns EF's `Task` directly without `async`/`await`, because there's nothing to do after the call. `GetAllAsync` needs `await` because it converts `List<StudySession>` to `IReadOnlyList<StudySession>`.

One rule to know: a `DbContext` is **not thread-safe**. Always `await` one call before starting the next on the same context. Never start two queries in parallel on one context, for example with `Task.WhenAll`.

## 12. Make the Service Async

Replace `Services/IStudySessionService.cs`:

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

and `Services/StudySessionService.cs`:

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

## 13. Make the Controller Async

Replace `Controllers/StudySessionsController.cs`:

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
// the connection, and passing it down lets the database stop working on an answer
// nobody is waiting for.
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
- The action names **keep** their names without `Async` (`GetById`, not `GetByIdAsync`). By default, MVC strips an `Async` suffix from action names, so `CreatedAtAction(nameof(GetByIdAsync), ...)` would point at an action called `GetByIdAsync` that doesn't exist, and `POST` would fail with "No route matches the supplied values".

## 14. Migrate and Seed Asynchronously

Two small changes finish the job. In `Data/SampleData.cs`, make the seeding async:

```csharp
using CoolApp.Models;
using Microsoft.EntityFrameworkCore;

namespace CoolApp.Data;

// The same two sample sessions InMemoryStudySessionRepository starts with, written to
// the database the first time the app starts with an empty table.
//
// Not a migration (HasData): migrations need fixed values, and these sessions start
// "tomorrow" and "the day after" relative to whenever you first run the app.
public static class SampleData
{
    public static async Task SeedAsync(StudySpotDbContext db)
    {
        if (await db.StudySessions.AnyAsync())
        {
            return;
        }

        db.StudySessions.AddRange(
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
            });
        await db.SaveChangesAsync();
    }
}
```

and in `Program.cs`, replace the two lines inside the `using (var scope = ...)` block:

```csharp
    await db.Database.MigrateAsync();
    await SampleData.SeedAsync(db);
```

Top-level statements in `Program.cs` can use `await` directly.

## 15. Checkpoint: Build and Try Everything Again

```pwsh
dotnet build
dotnet run
```

Run the `CoolApp.http` requests again. Nothing should behave differently:

- `GET /api/studysessions`, `GET /api/studysessions/1`, `GET /api/studysessions/999` (expect 404), `GET /api/studysessions/summary`.
- `POST` a valid session (201, with a `Location` header pointing at the new id), then an invalid one (400: validation still happens before the controller runs).
- `PUT` the new session and `GET` it back to confirm the change stuck. `PUT` a missing id (404).
- `PUT /api/studysessions/{id}/rating` with `4` (204), with `9` (400), on a missing id (404).
- `DELETE` it (204), then again (404).
- Filters: `?course=...`, `?minSeatsAvailable=...`, `?asOf=...`.
- Restart the app: everything you changed is still there.

## Common async Mistakes to Look For in Your Own Code

- **`.Result` or `.Wait()`** on a `Task`: blocks the thread anyway (no benefit from async) and can deadlock. Use `await`.
- **`async void`**: the caller can't await it, and an exception in it crashes the process. Use `async Task`. The only exception is event handlers.
- **Synchronous EF calls left behind** (`ToList()`, `SaveChanges()`, `First()` on a `DbSet`): search for them after the refactor. Lab Assignment #2 says *no* I/O is performed synchronously.
- **A token that never reaches the database**: accepted in the controller but not passed further, or a `= default` parameter that every caller skips. Follow one request from the controller to the `...Async(cancellationToken)` call in the repository.
- **`DbContext` used directly in controllers or minimal-API endpoints**: it works, but the HTTP layer now knows about the database and the rules have nowhere to live. Keep it behind your repository (or at least a service), as in Lecture 06.

## Next Steps

Update `ROADMAP.md` (the Beta items "Persist sessions in a database with Entity Framework" and "`async`/`await` for all I/O" can be checked off) and `README.md`'s "Project Map" and "Current Examples". Two things this walkthrough deliberately leaves for later:

- `GetAll` still loads **every** session and filters in memory. That's fine for two sessions, but with real data the filtering should happen in the database query. Note that the SQLite provider can't translate comparisons or ordering on `DateTimeOffset` columns, which is one reason to move to a "real" database server once queries get serious.
- The API still sends the EF entity itself over the wire. Request/response DTOs that separate the API contract from the database entity are on the ROADMAP for Beta.

See [WALKTHROUGHS.md](WALKTHROUGHS.md) for later lectures' walkthroughs as they're added.
