using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Api.Tests.MSTest.Unit;

// The MSTest version of CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceContractTests.cs.
[TestClass]
public class StudySessionServiceContractTests(TestContext testContext)
{
    private readonly StudySessionService _service = new(new CopyingStudySessionRepository());

    [TestMethod]
    public async Task Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var cancellation = testContext.CancellationToken;
        var session = await _service.CreateAsync(new StudySession
        {
            Course = "Software Development I",
            Topic = "Testing",
            Location = "MIF, room 401",
            StartsAt = new DateTimeOffset(2030, 1, 2, 18, 0, 0, TimeSpan.Zero),
            HostName = "Tomas",
            SeatsAvailable = 3
        }, cancellation);

        await _service.RateAsync(session.Id, 5, cancellation);

        var stored = await _service.GetByIdAsync(session.Id, cancellation);
        Assert.AreEqual(new Rating(5), stored!.HostRating);
    }
}
