using CoolApp.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CoolApp.Api.Tests.MSTest.Integration;

// The MSTest version of CoolApp.Api.Tests.XUnit/Integration/StudySpotApiFactory.cs. xUnit's
// assembly fixture becomes static [AssemblyInitialize]/[AssemblyCleanup] methods: they run
// once before the first test and once after the last one.
[TestClass]
public class StudySpotApi
{
    private static readonly string DatabasePath = Path.Combine(Path.GetTempPath(), $"studyspot-tests-{Guid.NewGuid()}.db");

    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;

    [AssemblyInitialize]
    public static void Start(TestContext _)
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:StudySpot", $"Data Source={DatabasePath}"));
        Factory.StartServer();
    }

    [AssemblyCleanup]
    public static async Task StopAsync()
    {
        await Factory.DisposeAsync();
        // SQLite keeps pooled connections open, which keeps the file locked on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }
}

[TestClass]
public class MigrationsTests
{
    [TestMethod]
    public void Model_HasNoChangesWithoutAMigration()
    {
        using var scope = StudySpotApi.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();

        Assert.IsFalse(db.Database.HasPendingModelChanges());
    }
}
