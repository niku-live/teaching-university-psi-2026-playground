# StudySpot Roadmap

This roadmap follows the Alpha / Beta / Final structure expected from your own lab project (see [Lab Assignment requirements](https://github.com/niku-live/teaching-university-psi-2026/blob/main/Lecture00/README.md#lab-assignment-requirements)).

## Alpha (in progress)

- [x] List available study sessions
- [x] Create a new study session via the API
- [ ] Create a session from the UI (currently API-only, demonstrated with the browser/curl/Postman)
- [ ] Basic input validation on the `StudySession` model

## Beta

- [ ] Join / RSVP to a session and track remaining seats
- [ ] Filter sessions by course
- [ ] Persist sessions in a database with Entity Framework, instead of the in-memory list
- [ ] Automated tests for the API endpoints

## Final

- [ ] User accounts, so sessions are tied to a real host
- [ ] Notifications/reminders before a session starts
- [ ] Session ratings, so good hosts stand out

## Non-goals (for this demo)

- This is a teaching example, not a real product &mdash; it intentionally stays small and skips things like authentication, deployment, and monitoring that your own lab project should eventually cover.
