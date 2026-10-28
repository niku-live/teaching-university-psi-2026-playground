using CoolApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoolApp.Api.Tests.XUnit.Integration;

public class MigrationsTests(StudySpotApiFactory factory)
{
    // Fails when an entity was changed but `dotnet ef migrations add` was not run, so a
    // forgotten migration is caught by the tests instead of by the first user.
    [Fact]
    public void Model_HasNoChangesWithoutAMigration()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StudySpotDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
    }
}
