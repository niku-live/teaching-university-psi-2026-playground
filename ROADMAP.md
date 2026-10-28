# StudySpot Roadmap

This roadmap follows the Alpha / Beta / Final structure expected from your own lab project, and maps StudySpot's own features onto the graded technical requirements from each [Lab Assignment](https://github.com/niku-live/teaching-university-psi-2026/blob/main/Lecture00/README.md#lab-assignment-requirements) - so "covering the requirements" means something concrete, not just a feature list.

## Alpha - matches Lab Assignment #1 (lectures 1-6)

Features:
- [x] List available study sessions
- [x] Create a new study session via the API
- [x] Create a session from the UI
- [x] Full CRUD on the API (update and delete a study session, not just list/create)
- [x] API documentation via Swagger/OpenAPI
- [x] Basic input validation on the `StudySession` model

Requirement coverage still needed:
- [x] A named `record` type (e.g. an immutable `StudySessionSummary`) alongside the existing `class`/`struct` usage
- [x] At least one `enum` (e.g. a `SessionStatus`)
- [x] Named and optional arguments in a real method signature
- [x] An extension method (e.g. `IEnumerable<StudySession>.UpcomingOnly()`)
- [x] LINQ used for filtering/sorting sessions
- [x] One standard .NET interface implemented (e.g. `IComparable<StudySession>` to sort by start time)

## Beta - matches Lab Assignment #2 (lectures 7-10)

Features:
- [ ] Join / RSVP to a session and track remaining seats
- [x] Filter sessions by course

Requirement coverage still needed:
- [x] Persist sessions in a database with Entity Framework, instead of the in-memory list (Lecture 09: SQLite, `StudySpotDbContext` + `EfStudySessionRepository` behind the existing `IStudySessionRepository`, migrations, integration tests against their own database)
- [ ] Request/response DTOs (e.g. `CreateStudySessionDto`/`StudySessionDto`) separating the API contract from the EF entity - moved to Lecture 10, now that there's a real entity (with tracking/navigation properties) worth not leaking over the wire; `StudySessionSummary` (Lecture 04) is a lightweight preview of the same idea
- [ ] A generic type/method (e.g. a generic `Repository<T>` used for `StudySession` and one other entity)
- [x] A custom exception type, thrown and handled meaningfully (Lecture 09: `StudySessionConflictException` when an edit is based on an outdated `Version`, turned into a `409 Conflict` for every endpoint by an `IExceptionHandler`; a `SessionFullException` can follow once RSVP exists)
- [ ] A shared memory scenario handled with a concurrent collection or a lock (planned for Lecture 10: the database removed the shared in-memory `List`, so this needs a new one, e.g. a cache)
- [x] `async`/`await` for all I/O (database calls, no synchronous DB access) (Lecture 08 made the repository, service and controller async with the request's `CancellationToken`; Lecture 09's Entity Framework repository uses only the async EF methods and passes the token to every query)
- [x] Dependency Injection used for the repository/service layer instead of `new`-ing dependencies (landed early in Lecture 06: `IStudySessionService`/`StudySessionService` + `IStudySessionRepository`, backed by an in-memory implementation until Entity Framework arrives)
- [x] Unit and integration test coverage of at least 50% (Lecture 08: `CoolApp.Api.Tests.XUnit` with xUnit v3 unit tests, controller tests with NSubstitute and `WebApplicationFactory` integration tests - about 80% line coverage of the backend, generated code excluded - plus Jest + React Testing Library tests for the session form, about 63% of the React code; about 71% combined)

## Final - matches Lab Assignment #3 (lectures 11-14)

Features:
- [ ] User accounts, so sessions are tied to a real host
- [ ] Notifications/reminders before a session starts
- [ ] Session ratings, so good hosts stand out (early building block landed: a `Rating` value type + a per-session `HostRating`, `PUT /api/studysessions/{id}/rating` - still missing who's allowed to rate, preventing duplicate ratings, and rolling ratings up across a host's sessions)

Requirement coverage still needed:
- [ ] Entity Framework migrations, run automatically (started in Lecture 09: migrations exist and run on startup in Development; production needs its own step)
- [ ] Unit and integration test coverage of at least 80%
- [ ] A CI pipeline gating pull requests (tests + at least one extra gate)
- [ ] Live metrics/monitoring (OpenTelemetry or similar)

## Future Ideas (not yet scheduled to a phase)

- Location-based search ("sessions near me") - would need a `Coordinates` value type (e.g. `record struct Coordinates(double Latitude, double Longitude)`) replacing or augmenting `Location`'s free-text string, plus real geocoding/distance-calculation data this demo doesn't currently have.

## Non-goals (for this demo)

- This is a teaching example, not a real product - it intentionally skips things like the video walkthrough and bonus deployment/observability points, which apply to your own team project rather than this shared classroom demo.
