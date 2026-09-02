# PSI 2026 Playground

This repository is the live demonstration project for the **Software Development I (PSI) 2026 Fall** course. It starts as a small ASP.NET Core and React application and will evolve during lectures as we explore language features, architecture, testing, collaboration, and development practices.

> [!WARNING]
> The code is written for teaching. Some commits may deliberately demonstrate incomplete solutions, trade-offs, or bad practices. Do not treat the repository as a production template.

Course notes and lecture materials are maintained in the [PSI 2026 course repository](https://github.com/niku-live/PSI2026). Student teams should follow the assignment requirements there rather than copy this demo project as their submission.

## Technology Baseline

- .NET 10 LTS and ASP.NET Core
- C# with nullable reference types enabled
- React 18 and JavaScript
- Bootstrap 5

The frontend was inherited from the older ASP.NET Core React template and still uses Create React App. Keeping that implementation visible gives us a realistic codebase to inspect and improve during the course.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A current [Node.js LTS](https://nodejs.org/) release with npm
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the ASP.NET and web development workload, or [Visual Studio Code](https://code.visualstudio.com/) with C# Dev Kit
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
| `Controllers/` | Backend API controllers |
| `Pages/` | Server-rendered error page support |
| `ClientApp/src/` | React application source |
| `ClientApp/src/components/` | Example UI components |
| `appsettings*.json` | Backend configuration |
| `Properties/launchSettings.json` | Local development profiles and URLs |

## Current Examples

- A minimal ASP.NET Core API returning weather forecast data
- React routing and reusable components
- Client-to-server API calls through the development proxy
- A stateful counter component
- Development and production SPA build integration

The repository will change throughout the semester. Use Git history to compare lecture stages and understand why each change was introduced.
