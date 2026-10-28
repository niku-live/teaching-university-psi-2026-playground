using CoolApp.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CoolApp.Api.Tests.NUnit.Integration;

// The NUnit version of CoolApp.Api.Tests.XUnit/Integration/StudySpotApiFactory.cs. xUnit's
// assembly fixture becomes a [SetUpFixture]: its [OneTimeSetUp] runs once before every
// test in this namespace, and [OneTimeTearDown] once after the last one.
[SetUpFixture]
public class StudySpotApi
{
    private static readonly string DatabasePath = Path.Combine(Path.GetTempPath(), $"studyspot-tests-{Guid.NewGuid()}.db");

    public static WebApplicationFactory<Program> Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public void Start()
    {
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:StudySpot", $"Data Source={DatabasePath}"));
        Factory.StartServer();
    }

    [OneTimeTearDown]
    public async Task StopAsync()
    {
        await Factory.DisposeAsync();
        // SQLite keeps pooled connections open, which keeps the file locked on Windows.
        SqliteConnection.ClearAllPools();
        File.Delete(DatabasePath);
    }
}

public class MigrationsTests
{
    [Test]
    public void Model_HasNoChangesWithoutAMigration()
    {
        using var scope = StudySpotApi.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();

        Assert.That(db.Database.HasPendingModelChanges(), Is.False);
    }
}
