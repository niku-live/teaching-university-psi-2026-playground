using CoolApp.Models;
using CoolApp.Services;

namespace CoolApp.Api.Tests.NUnit.Unit;

// The NUnit version of CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceContractTests.cs.
// The service is created per test in [SetUp] - see StudySessionServiceTests for why.
public class StudySessionServiceContractTests
{
    private StudySessionService _service = null!;

    [SetUp]
    public void SetUp() => _service = new StudySessionService(new CopyingStudySessionRepository());

    [Test]
    public async Task Rate_RepositoryReturnsCopies_RatingIsStillSaved()
    {
        var cancellation = TestContext.CurrentContext.CancellationToken;
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
        Assert.That(stored!.HostRating, Is.EqualTo(new Rating(5)));
    }
}
