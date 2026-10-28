using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// The same service, run against a repository that returns copies. These tests only pass
// because the service saves every change through repository.Update - delete that line
// in StudySessionService.Rate and watch this test fail, while the tests in
// StudySessionServiceTests (using the in-memory repository, which hands out the stored
// instance) still pass.
public class StudySessionServiceContractTests
{
    private readonly StudySessionService _service = new(new CopyingStudySessionRepository());

    [Fact]
    public void Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var session = _service.Create(new StudySession
        {
            Course = "Software Development I",
            Topic = "Testing",
            Location = "MIF, room 401",
            StartsAt = new DateTimeOffset(2030, 1, 2, 18, 0, 0, TimeSpan.Zero),
            HostName = "Tomas",
            SeatsAvailable = 3
        });

        _service.Rate(session.Id, 5);

        Assert.Equal(new Rating(5), _service.GetById(session.Id)!.HostRating);
    }
}
