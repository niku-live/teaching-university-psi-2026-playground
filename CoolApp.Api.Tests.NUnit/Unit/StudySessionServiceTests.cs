using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Api.Tests.NUnit.Unit;

// The NUnit version of CoolApp.Api.Tests.XUnit/Unit/StudySessionServiceTests.cs - same tests,
// different framework. What changes:
//   [Fact] -> [Test], [Theory] + [InlineData] -> [TestCase]
//   Assert.Equal(expected, actual) -> Assert.That(actual, Is.EqualTo(expected))
//   TestContext.Current.CancellationToken -> TestContext.CurrentContext.CancellationToken
//   the constructor -> a [SetUp] method (see below)
public class StudySessionServiceTests
{
    private static readonly DateTimeOffset AsOf = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // NUnit creates ONE instance of this class and runs every test on it. A field
    // initializer would therefore give all tests the same repository, and sessions added
    // by one test would show up in the next. [SetUp] runs before each test and creates a
    // fresh one. (xUnit and MSTest create a new instance per test, so they don't need this.)
    private InMemoryStudySessionRepository _repository = null!;
    private StudySessionService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = new InMemoryStudySessionRepository();
        _service = new StudySessionService(_repository);
    }

    private static CancellationToken Cancellation => TestContext.CurrentContext.CancellationToken;

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

    [Test]
    public async Task GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        await AddSessionAsync("Before the cutoff", AsOf.AddDays(-1));
        await AddSessionAsync("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        // Assert
        Assert.That(upcoming.Select(s => s.Topic), Is.EqualTo(new[] { "After the cutoff" }));
    }

    [Test]
    public async Task GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        await AddSessionAsync("Third", AsOf.AddDays(3));
        await AddSessionAsync("First", AsOf.AddDays(1));
        await AddSessionAsync("Second", AsOf.AddDays(2));

        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.That(upcoming.Select(s => s.Topic), Is.EqualTo(new[] { "First", "Second", "Third" }));
    }

    [TestCase("software", 1)]
    [TestCase("SOFTWARE DEVELOPMENT", 1)]
    [TestCase("Algorithms", 0)]
    public async Task GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        await AddSessionAsync("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = await _service.GetUpcomingAsync(course, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.That(upcoming.Count(), Is.EqualTo(expectedCount));
    }

    [Test]
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

        Assert.That(updated, Is.True);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        // Assert.Multiple reports every failed assertion in the block, not just the first.
        Assert.Multiple(() =>
        {
            Assert.That(stored.Topic, Is.EqualTo("New topic"));
            Assert.That(stored.SeatsAvailable, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task Update_UnknownId_ReturnsFalse()
    {
        var updated = await _service.UpdateAsync(999, new StudySession(), Cancellation);

        Assert.That(updated, Is.False);
    }

    [Test]
    public async Task Rate_ExistingSession_StoresTheRating()
    {
        var session = await AddSessionAsync("Rated session", AsOf.AddDays(1));

        var rated = await _service.RateAsync(session.Id, 4, Cancellation);

        Assert.That(rated, Is.True);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.That(stored.HostRating, Is.EqualTo(new Rating(4)));
    }

    [Test]
    public async Task Rate_UnknownId_ReturnsFalse()
    {
        Assert.That(await _service.RateAsync(999, 4, Cancellation), Is.False);
    }

    [Test]
    public async Task GetUpcoming_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // CatchAsync accepts the exception type and its subclasses (like xUnit's
        // ThrowsAnyAsync); ThrowsAsync would require exactly OperationCanceledException.
        Assert.CatchAsync<OperationCanceledException>(
            () => _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, cancellation.Token));
    }
}
