# StudySpot Roadmap

This roadmap follows the Alpha / Beta / Final structure expected from your own lab project, and maps StudySpot's own features onto the graded technical requirements from each [Lab Assignment](https://github.com/niku-live/teaching-university-psi-2026/blob/main/Lecture00/README.md#lab-assignment-requirements) - so "covering the requirements" means something concrete, not just a feature list.

## Alpha - matches Lab Assignment #1 (lectures 1-6)

Features:
- [x] List available study sessions
- [x] Create a new study session via the API
- [ ] Create a session from the UI (currently API-only, demonstrated with the browser/curl/Postman)
- [ ] Basic input validation on the `StudySession` model

Requirement coverage still needed:
- [ ] A named `record` type (e.g. an immutable `StudySessionSummary`) alongside the existing `class`/`struct` usage
- [ ] At least one `enum` (e.g. a `SessionStatus`)
- [ ] Named and optional arguments in a real method signature
- [ ] An extension method (e.g. `IEnumerable<StudySession>.UpcomingOnly()`)
- [ ] LINQ used for filtering/sorting sessions
- [ ] One standard .NET interface implemented (e.g. `IComparable<StudySession>` to sort by start time)

## Beta - matches Lab Assignment #2 (lectures 7-10)

Features:
- [ ] Join / RSVP to a session and track remaining seats
- [ ] Filter sessions by course

Requirement coverage still needed:
- [ ] Persist sessions in a database with Entity Framework, instead of the in-memory list
- [ ] A generic type/method (e.g. a generic `Repository<T>` used for `StudySession` and one other entity)
- [ ] A custom exception type, thrown and handled meaningfully (e.g. `SessionFullException` when RSVP-ing to a full session)
- [ ] `async`/`await` for all I/O (database calls, no synchronous DB access)
- [ ] Dependency Injection used for the repository/service layer instead of `new`-ing dependencies
- [ ] Unit and integration test coverage of at least 50%

## Final - matches Lab Assignment #3 (lectures 11-14)

Features:
- [ ] User accounts, so sessions are tied to a real host
- [ ] Notifications/reminders before a session starts
- [ ] Session ratings, so good hosts stand out

Requirement coverage still needed:
- [ ] Entity Framework migrations, run automatically
- [ ] Unit and integration test coverage of at least 80%
- [ ] A CI pipeline gating pull requests (tests + at least one extra gate)
- [ ] Live metrics/monitoring (OpenTelemetry or similar)

## Non-goals (for this demo)

- This is a teaching example, not a real product - it intentionally skips things like the video walkthrough and bonus deployment/observability points, which apply to your own team project rather than this shared classroom demo.
