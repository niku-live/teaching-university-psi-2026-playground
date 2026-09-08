# From a Blank Computer to a Running Project

This is the step-by-step walkthrough behind [Lecture 01](https://github.com/niku-live/teaching-university-psi-2026/tree/main/Lecture01): everything needed to go from an empty machine to a running ASP.NET Core + React project, before any of the "make it your own" changes described in the main [README](README.md).

## 1. Prepare Your Computer

Install these once, regardless of which IDE you end up using:

- **[.NET SDK](https://dotnet.microsoft.com/download)** - matching the version in this repo's `.csproj` (currently .NET 10). Verify with `dotnet --version`.
- **[Git](https://git-scm.com/downloads)** - verify with `git --version`.
- **[Node.js LTS](https://nodejs.org/)** with npm - required to build the React frontend. Verify with `node --version` and `npm --version`.
- A trusted local HTTPS certificate: `dotnet dev-certs https --trust`.

## 2. Install an IDE

Pick **one** of the following (all three work with this course's projects):

### a) Visual Studio (Windows)

1. Install [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (Community edition is free).
2. In the installer, select the **ASP.NET and web development** workload.
3. The Node.js tools and NuGet package management come bundled with this workload.

### b) Visual Studio Code (Windows/macOS/Linux)

1. Install [Visual Studio Code](https://code.visualstudio.com/).
2. Install the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension (this also installs the base C# extension and IntelliCode).
3. Optional but recommended for this course: the [Quarto extension](https://marketplace.visualstudio.com/items?itemName=quarto.quarto), used for some `.qmd` lecture material.

### c) JetBrains Rider (Windows/macOS/Linux)

1. Install [JetBrains Rider](https://www.jetbrains.com/rider/) (free for students with a JetBrains education license, or via [JetBrains Toolbox](https://www.jetbrains.com/toolbox-app/)).
2. Rider bundles .NET and JavaScript/TypeScript support out of the box - no extra plugins are required for this template.

## 3. Create a New Project From the ASP.NET Core + React Template

The starting point for this repository (before the changes described in the main [README](README.md)) is the built-in ASP.NET Core + React template. Run `dotnet new list` first to confirm the exact template name available on your installed SDK version - the steps below assume it's still named `react`.

### a) Command line (PowerShell / Bash)

```bash
dotnet new react -n YourAppName
cd YourAppName
dotnet dev-certs https --trust
dotnet run
```

`dotnet run` restores the frontend's npm packages on first build and starts both the ASP.NET Core backend and the React dev server together (through the SPA proxy).

### b) Visual Studio

1. **File > New > Project...**
2. Search for **"React"** and select the **ASP.NET Core with React.js** template (or **ASP.NET Core Web App** and choose the React frontend framework in the next step, depending on your Visual Studio version).
3. Name the project and choose a location.
4. Press **F5** (or the green Run button) to build and start debugging.

### c) Visual Studio Code

VS Code doesn't have a project-creation wizard, so create the project from the integrated terminal the same way as the command-line steps above (`dotnet new react -n YourAppName`), then **File > Open Folder...** on the generated folder. The C# Dev Kit extension will pick up the `.csproj` automatically and offer to restore/build/run it.

### d) JetBrains Rider

1. **File > New Solution...**
2. Choose **ASP.NET Core Web Application**, then pick **React.js** as the frontend framework in the template options.
3. Name the solution/project and choose a location.
4. Use the **Run** button in the toolbar to build and start the app.

## Next Steps

Once your project builds and runs with the default template content, follow the [main README](README.md) and [ROADMAP.md](ROADMAP.md) to see how this repository turned the same starting point into StudySpot - then do the same for your own team project (see the [Lecture 01 homework](https://github.com/niku-live/teaching-university-psi-2026/blob/main/Lecture01/TODO-LIST.md)).
