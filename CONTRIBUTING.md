# Contributing to StudySpot

This is the live demonstration project for [PSI 2026](https://github.com/niku-live/teaching-university-psi-2026), so "contributing" here mostly means the lecturer live-coding during class - but the process below is written the way a real team's would be. Copy this file (and adjust it) for your own team's project.

## Before You Start

See [WALKTHROUGH-01.md](WALKTHROUGH-01.md) for environment setup, and [`docs/definition-of-done.md`](docs/definition-of-done.md) for what "finished" means here.

## Branch Naming

Pattern: `[initials]/[issue-number]-[short-title]`

Example &mdash; Tomas picks up issue #12, "Add session search":

`tj/12-add-session-search`

- Initials first (lowercase), separated from the rest by `/`.
- Issue number next, then a short kebab-case description of the task.
- Branch off `main`; open a pull request back into `main` when ready for review.

## Code Formatting

- **Backend (C#):** run `dotnet format` before committing so contributions read the same regardless of which IDE wrote them.
- **Frontend (JavaScript):** match the style already used under `ClientApp/src` - most editors pick this up automatically if you have a formatter (Prettier/ESLint) configured.
- Whichever IDE you use (Visual Studio, VS Code, or Rider - see [WALKTHROUGH-01.md](WALKTHROUGH-01.md)), turning on "format on save" makes this automatic instead of a manual step before every commit.

## Opening a Pull Request

- Every change ships as a pull request into `main` - no direct pushes, even for small fixes.
- GitHub pre-fills the description from [`.github/pull_request_template.md`](.github/pull_request_template.md) - fill it in for real, don't leave the placeholders.
- [`CODEOWNERS`](.github/CODEOWNERS) auto-requests review from whoever owns the part of the codebase you touched - see that file for who owns what.
- Before marking it ready for review, check it against [`docs/definition-of-done.md`](docs/definition-of-done.md).

## Filing Issues

Use GitHub Issues for anything that isn't ready to be a pull request yet - a bug, a roadmap item worth tracking, a question. A short, specific title beats a detailed body nobody reads; add detail in the first comment if it's needed.
