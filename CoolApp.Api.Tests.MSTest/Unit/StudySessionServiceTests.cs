using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Api.Tests.MSTest.Unit;

// The MSTest version of CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceTests.cs - same
// tests, different framework. What changes:
//   [TestClass] on the class, [Fact] -> [TestMethod], [Theory] + [InlineData] -> [TestMethod] + [DataRow]
//   Assert.Equal(expected, actual) -> Assert.AreEqual(expected, actual)
//   TestContext.Current.CancellationToken -> testContext.CancellationToken, where MSTest
//   passes the TestContext in through the constructor
// Like xUnit, MSTest creates a new instance of the class for every test, so the field
// initializer gives every test its own repository.
[TestClass]
public class StudySessionServiceTests(TestContext testContext)
{
    private static readonly DateTimeOffset AsOf = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly StudySessionService _service = new(new InMemoryStudySessionRepository());

    private CancellationToken Cancellation => testContext.CancellationToken;

    private Task<StudySession> AddSessionAsync(string topic, DateTimeOffset startsAt, string course = "Software Development I", int seats = 5) =>
        _service.CreateAsync(new StudySession
        {
            Course = course,
            Topic = topic,
            Location = "MIF, room 401",
            StartsAt = startsAt,
            HostName = "Ieva",
            SeatsAvailable = seats
        }, Cancellation);

    [TestMethod]
    public async Task GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        await AddSessionAsync("Before the cutoff", AsOf.AddDays(-1));
        await AddSessionAsync("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        // Assert - CollectionAssert compares two ICollections item by item, so the LINQ
        // result is turned into a list first.
        CollectionAssert.AreEqual(new[] { "After the cutoff" }, upcoming.Select(s => s.Topic).ToList());
    }

    [TestMethod]
    public async Task GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        await AddSessionAsync("Third", AsOf.AddDays(3));
        await AddSessionAsync("First", AsOf.AddDays(1));
        await AddSessionAsync("Second", AsOf.AddDays(2));

        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        CollectionAssert.AreEqual(new[] { "First", "Second", "Third" }, upcoming.Select(s => s.Topic).ToList());
    }

    [TestMethod]
    [DataRow("software", 1)]
    [DataRow("SOFTWARE DEVELOPMENT", 1)]
    [DataRow("Algorithms", 0)]
    public async Task GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        await AddSessionAsync("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = await _service.GetUpcomingAsync(course, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.AreEqual(expectedCount, upcoming.Count());
    }

    [TestMethod]
    public async Task Update_ExistingSession_ChangeIsVisibleThroughGetById()
    {
        var session = await AddSessionAsync("Old topic", AsOf.AddDays(1));
        var changed = new StudySession
        {
            Course = session.Course,
            Topic = "New topic",
            Location = session.Location,
            StartsAt = session.StartsAt,
            HostName = session.HostName,
            SeatsAvailable = 2
        };

        var updated = await _service.UpdateAsync(session.Id, changed, Cancellation);

        Assert.IsTrue(updated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.AreEqual("New topic", stored.Topic);
        Assert.AreEqual(2, stored.SeatsAvailable);
    }

    [TestMethod]
    public async Task Update_UnknownId_ReturnsFalse()
    {
        var updated = await _service.UpdateAsync(999, new StudySession(), Cancellation);

        Assert.IsFalse(updated);
    }

    [TestMethod]
    public async Task Rate_ExistingSession_StoresTheRating()
    {
        var session = await AddSessionAsync("Rated session", AsOf.AddDays(1));

        var rated = await _service.RateAsync(session.Id, 4, Cancellation);

        Assert.IsTrue(rated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.AreEqual(new Rating(4), stored.HostRating);
    }

    [TestMethod]
    public async Task Rate_UnknownId_ReturnsFalse()
    {
        Assert.IsFalse(await _service.RateAsync(999, 4, Cancellation));
    }

    [TestMethod]
    public async Task GetUpcoming_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // ThrowsAsync accepts the exception type and its subclasses (like xUnit's
        // ThrowsAnyAsync); ThrowsExactlyAsync would require exactly OperationCanceledException.
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, cancellation.Token));
    }
}
