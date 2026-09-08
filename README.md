# StudySpot

StudySpot is a small app for finding and hosting study sessions for your courses: see what's on this week, or add your own session for others to join.

> [!WARNING]
> This is the **live demonstration project** for the **Software Development I (PSI) 2026 Fall** course, not a production app. Some commits deliberately show incomplete solutions, trade-offs, or bad practices for teaching purposes. Course notes and lecture materials are maintained in the [PSI 2026 course repository](https://github.com/niku-live/teaching-university-psi-2026); student teams should follow the assignment requirements there rather than copy this repository as their submission.

## From Template to Product

New to the template? [WALKTHROUGH.md](WALKTHROUGH.md) covers preparing your computer (SDKs, Git, an IDE) and creating a new project from the ASP.NET Core + React template from scratch, across the command line, Visual Studio, VS Code, and Rider.

This repository started from that plain template (see the [`lectures/00`](https://github.com/niku-live/teaching-university-psi-2026-playground/tree/lectures/00) branch for that starting point). During [Lecture 01](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture01) we turned it into the beginning of StudySpot by:

1. Writing this README to describe the actual product, instead of the template's generic scaffolding text.
2. Writing a [ROADMAP.md](ROADMAP.md) with Alpha/Beta/Final scope.
3. Replacing the placeholder landing page with real copy about StudySpot.
4. Replacing the sample `WeatherForecast` model + endpoint with a real `StudySession` model and API (`Models/StudySession.cs`, `Controllers/StudySessionsController.cs`).
5. Deleting template boilerplate we no longer need (the `Counter` demo page and the old `FetchData` page).

Your own team repository should go through the same steps this week &mdash; see the [Lecture 01 homework](https://github.com/niku-live/teaching-university-psi-2026/blob/main/Lecture01/TODO-LIST.md).

## Roadmap

See [ROADMAP.md](ROADMAP.md).

## Technology Baseline

- .NET 10 LTS and ASP.NET Core
- C# with nullable reference types enabled
- React 18 and JavaScript
- Bootstrap 5

The frontend was inherited from the older ASP.NET Core React template and still uses Create React App. Keeping that implementation visible gives us a realistic codebase to inspect and improve during the course.

## Prerequisites

See [WALKTHROUGH.md](WALKTHROUGH.md) for a guided setup. In short, you'll need:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A current [Node.js LTS](https://nodejs.org/) release with npm
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the ASP.NET and web development workload, [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit, or JetBrains Rider
- A trusted ASP.NET Core development certificate: `dotnet dev-certs https --trust`

## Build and Run

From the repository root:

```pwsh
dotnet restore
dotnet build
dotnet run
```

On the first Debug build, MSBuild installs the frontend dependencies. Running the backend starts the React development server through the ASP.NET Core SPA proxy. Open the HTTPS address printed by `dotnet run` (the default project profile uses `https://localhost:7039`).

Useful frontend commands can also be run directly:

```pwsh
cd ClientApp
npm ci
npm test
npm run build
```

## Project Map

| Path | Purpose |
| --- | --- |
| `Program.cs` | Configures the ASP.NET Core application and HTTP pipeline |
| `Models/` | Backend domain models |
| `Controllers/` | Backend API controllers |
| `Pages/` | Server-rendered error page support |
| `ClientApp/src/` | React application source |
| `ClientApp/src/components/` | Example UI components |
| `appsettings*.json` | Backend configuration |
| `Properties/launchSettings.json` | Local development profiles and URLs |

## Current Examples

- A minimal ASP.NET Core API returning and creating study sessions (`GET`/`POST /api/studysessions`)
- React routing and reusable components
- Client-to-server API calls through the development proxy
- Development and production SPA build integration

The repository will change throughout the semester. Use Git history to compare lecture stages and understand why each change was introduced.
