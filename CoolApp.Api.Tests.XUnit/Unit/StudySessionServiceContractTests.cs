using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Api.Tests.XUnit.Unit;

// The same service, run against a repository that returns copies. These tests only pass
// because the service saves every change through repository.UpdateAsync - delete that
// line in StudySessionService.RateAsync and watch this test fail, while the tests in
// StudySessionServiceTests (using the in-memory repository, which hands out the stored
// instance) still pass.
public class StudySessionServiceContractTests
{
    private readonly StudySessionService _service = new(new CopyingStudySessionRepository());

    [Fact]
    public async Task Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var session = await _service.CreateAsync(new StudySession
        {
            Course = "Software Development I",
            Topic = "Testing",
            Location = "MIF, room 401",
            StartsAt = new DateTimeOffset(2030, 1, 2, 18, 0, 0, TimeSpan.Zero),
            HostName = "Tomas",
            SeatsAvailable = 3
        }, TestContext.Current.CancellationToken);

        await _service.RateAsync(session.Id, 5, TestContext.Current.CancellationToken);

        var stored = await _service.GetByIdAsync(session.Id, TestContext.Current.CancellationToken);
        Assert.Equal(new Rating(5), stored!.HostRating);
    }
}
