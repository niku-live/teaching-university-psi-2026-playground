using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;

namespace CoolApp.Tests.Unit;

// Tests StudySessionService directly - no HTTP, no web server. The "unit" is the service
// together with the real InMemoryStudySessionRepository: it is fast and deterministic, so
// there is no reason to replace it with a test double.
public class StudySessionServiceTests
{
    // A fixed moment far after the repository's sample sessions (which start "tomorrow"),
    // so every test controls exactly which sessions count as upcoming - no real clock.
    private static readonly DateTimeOffset AsOf = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly InMemoryStudySessionRepository _repository = new();
    private readonly StudySessionService _service;

    // xUnit creates a new instance of the class for every test, so every test gets its
    // own repository and nothing leaks from one test into the next.
    public StudySessionServiceTests()
    {
        _service = new StudySessionService(_repository);
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

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

    [Fact]
    public async Task GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        await AddSessionAsync("Before the cutoff", AsOf.AddDays(-1));
        await AddSessionAsync("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        // Assert
        Assert.Equal(["After the cutoff"], upcoming.Select(s => s.Topic));
    }

    [Fact]
    public async Task GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        await AddSessionAsync("Third", AsOf.AddDays(3));
        await AddSessionAsync("First", AsOf.AddDays(1));
        await AddSessionAsync("Second", AsOf.AddDays(2));

        var upcoming = await _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.Equal(["First", "Second", "Third"], upcoming.Select(s => s.Topic));
    }

    // One method, three test cases: the course filter is a case-insensitive "contains".
    [Theory]
    [InlineData("software", 1)]
    [InlineData("SOFTWARE DEVELOPMENT", 1)]
    [InlineData("Algorithms", 0)]
    public async Task GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        await AddSessionAsync("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = await _service.GetUpcomingAsync(course, minSeatsAvailable: null, asOf: AsOf, Cancellation);

        Assert.Equal(expectedCount, upcoming.Count());
    }

    [Fact]
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

        Assert.True(updated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.Equal("New topic", stored.Topic);
        Assert.Equal(2, stored.SeatsAvailable);
    }

    [Fact]
    public async Task Update_UnknownId_ReturnsFalse()
    {
        var updated = await _service.UpdateAsync(999, new StudySession(), Cancellation);

        Assert.False(updated);
    }

    [Fact]
    public async Task Rate_ExistingSession_StoresTheRating()
    {
        var session = await AddSessionAsync("Rated session", AsOf.AddDays(1));

        var rated = await _service.RateAsync(session.Id, 4, Cancellation);

        Assert.True(rated);
        var stored = (await _service.GetByIdAsync(session.Id, Cancellation))!;
        Assert.Equal(new Rating(4), stored.HostRating);
    }

    [Fact]
    public async Task Rate_UnknownId_ReturnsFalse()
    {
        Assert.False(await _service.RateAsync(999, 4, Cancellation));
    }

    // Cancellation is part of the contract too: a request that was already cancelled
    // (the client disconnected) must not do any work.
    [Fact]
    public async Task GetUpcoming_CancelledToken_ThrowsOperationCanceledException()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _service.GetUpcomingAsync(course: null, minSeatsAvailable: null, asOf: AsOf, cancellation.Token));
    }
}
