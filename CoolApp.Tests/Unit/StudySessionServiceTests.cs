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

    private StudySession AddSession(string topic, DateTimeOffset startsAt, string course = "Software Development I", int seats = 5) =>
        _service.Create(new StudySession
        {
            Course = course,
            Topic = topic,
            Location = "MIF, room 401",
            StartsAt = startsAt,
            HostName = "Ieva",
            SeatsAvailable = seats
        });

    [Fact]
    public void GetUpcoming_SessionsBeforeAsOf_AreLeftOut()
    {
        // Arrange
        AddSession("Before the cutoff", AsOf.AddDays(-1));
        AddSession("After the cutoff", AsOf.AddDays(1));

        // Act
        var upcoming = _service.GetUpcoming(course: null, minSeatsAvailable: null, asOf: AsOf);

        // Assert
        Assert.Equal(["After the cutoff"], upcoming.Select(s => s.Topic));
    }

    [Fact]
    public void GetUpcoming_SessionsAddedOutOfOrder_AreSortedByStartTime()
    {
        AddSession("Third", AsOf.AddDays(3));
        AddSession("First", AsOf.AddDays(1));
        AddSession("Second", AsOf.AddDays(2));

        var upcoming = _service.GetUpcoming(course: null, minSeatsAvailable: null, asOf: AsOf);

        Assert.Equal(["First", "Second", "Third"], upcoming.Select(s => s.Topic));
    }

    // One method, three test cases: the course filter is a case-insensitive "contains".
    [Theory]
    [InlineData("software", 1)]
    [InlineData("SOFTWARE DEVELOPMENT", 1)]
    [InlineData("Algorithms", 0)]
    public void GetUpcoming_CourseFilter_MatchesPartOfTheNameIgnoringCase(string course, int expectedCount)
    {
        AddSession("Pull requests", AsOf.AddDays(1), course: "Software Development I");

        var upcoming = _service.GetUpcoming(course, minSeatsAvailable: null, asOf: AsOf);

        Assert.Equal(expectedCount, upcoming.Count());
    }

    [Fact]
    public void Update_ExistingSession_ChangeIsVisibleThroughGetById()
    {
        var session = AddSession("Old topic", AsOf.AddDays(1));
        var changed = new StudySession
        {
            Course = session.Course,
            Topic = "New topic",
            Location = session.Location,
            StartsAt = session.StartsAt,
            HostName = session.HostName,
            SeatsAvailable = 2
        };

        var updated = _service.Update(session.Id, changed);

        Assert.True(updated);
        var stored = _service.GetById(session.Id)!;
        Assert.Equal("New topic", stored.Topic);
        Assert.Equal(2, stored.SeatsAvailable);
    }

    [Fact]
    public void Update_UnknownId_ReturnsFalse()
    {
        var updated = _service.Update(999, new StudySession());

        Assert.False(updated);
    }

    [Fact]
    public void Rate_ExistingSession_StoresTheRating()
    {
        var session = AddSession("Rated session", AsOf.AddDays(1));

        var rated = _service.Rate(session.Id, 4);

        Assert.True(rated);
        Assert.Equal(new Rating(4), _service.GetById(session.Id)!.HostRating);
    }

    [Fact]
    public void Rate_UnknownId_ReturnsFalse()
    {
        Assert.False(_service.Rate(999, 4));
    }
}
