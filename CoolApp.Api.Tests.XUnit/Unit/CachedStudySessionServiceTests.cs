using CoolApp.Caching;
using CoolApp.Models;
using CoolApp.Repositories;
using CoolApp.Services;
using NSubstitute;

namespace CoolApp.Api.Tests.XUnit.Unit;

// The caching decorator around the real StudySessionService.
public class CachedStudySessionServiceTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static StudySession Session(string topic) => new()
    {
        Course = "Software Development I",
        Topic = topic,
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };

    // A mock repository: counts how often the session is really read.
    [Fact]
    public async Task GetById_Twice_ReadsTheRepositoryOnce()
    {
        var repository = Substitute.For<IStudySessionRepository>();
        repository.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(Session("Caching"));
        var service = new CachedStudySessionService(new StudySessionService(repository), new InMemoryCache<int, StudySession?>());

        await service.GetByIdAsync(7, Cancellation);
        await service.GetByIdAsync(7, Cancellation);

        await repository.Received(1).GetByIdAsync(7, Arg.Any<CancellationToken>());
    }

    // The copying fake hands out copies, like a database: without the cache.Remove in
    // UpdateAsync, the second read would still return the cached "Before" copy.
    [Fact]
    public async Task Update_NextGetById_ReturnsTheChange()
    {
        var service = new CachedStudySessionService(
            new StudySessionService(new CopyingStudySessionRepository()), new InMemoryCache<int, StudySession?>());
        var created = await service.CreateAsync(Session("Before"), Cancellation);
        await service.GetByIdAsync(created.Id, Cancellation);

        await service.UpdateAsync(created.Id, Session("After"), Cancellation);
        var read = await service.GetByIdAsync(created.Id, Cancellation);

        Assert.Equal("After", read!.Topic);
    }
}
