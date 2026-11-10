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
