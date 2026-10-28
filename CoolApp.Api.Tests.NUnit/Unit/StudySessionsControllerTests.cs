using CoolApp.Controllers;
using CoolApp.Models;
using CoolApp.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoolApp.Api.Tests.NUnit.Unit;

// The NUnit version of CoolApp.Api.Tests.XUnit/Unit/StudySessionsControllerTests.cs. NSubstitute
// doesn't care which test framework runs it, so the stubs and mocks look exactly the same.
public class StudySessionsControllerTests
{
    private IStudySessionService _service = null!;
    private StudySessionsController _controller = null!;

    [SetUp]
    public void SetUp()
    {
        _service = Substitute.For<IStudySessionService>();
        _controller = new StudySessionsController(_service);
    }

    private static CancellationToken Cancellation => TestContext.CurrentContext.CancellationToken;

    private static StudySession Session(int id) => new()
    {
        Id = id,
        Course = "Software Development I",
        Topic = "Controller tests",
        Location = "MIF, room 401",
        StartsAt = new DateTimeOffset(2030, 1, 1, 10, 0, 0, TimeSpan.Zero),
        HostName = "Ieva",
        SeatsAvailable = 4
    };

    [Test]
    public async Task GetById_ServiceFindsTheSession_ReturnsIt()
    {
        var session = Session(7);
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _controller.GetById(7, Cancellation);

        Assert.That(result.Value, Is.SameAs(session));
    }

    [Test]
    public async Task GetById_ServiceReturnsNull_Returns404()
    {
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns((StudySession?)null);

        var result = await _controller.GetById(7, Cancellation);

        Assert.That(result.Result, Is.TypeOf<NotFoundResult>());
    }

    [Test]
    public async Task Create_ReturnsCreatedAtGetByIdWithTheNewId()
    {
        var created = Session(42);
        _service.CreateAsync(Arg.Any<StudySession>(), Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.Create(Session(0), Cancellation);

        var createdAt = result.Result as CreatedAtActionResult;
        Assert.That(createdAt, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(createdAt!.ActionName, Is.EqualTo(nameof(StudySessionsController.GetById)));
            Assert.That(createdAt.RouteValues!["id"], Is.EqualTo(42));
            Assert.That(createdAt.Value, Is.SameAs(created));
        });
    }

    [TestCase(true, typeof(NoContentResult))]
    [TestCase(false, typeof(NotFoundResult))]
    public async Task RateSession_ServiceResult_MapsToStatusCode(bool rated, Type expected)
    {
        _service.RateAsync(7, 5, Arg.Any<CancellationToken>()).Returns(rated);

        var result = await _controller.RateSession(7, new RatingSubmission(5), Cancellation);

        Assert.That(result, Is.TypeOf(expected));
    }

    [Test]
    public async Task GetAll_PassesTheFiltersAndTheTokenToTheService()
    {
        using var request = new CancellationTokenSource();

        await _controller.GetAll(request.Token, course: "PSI", minSeatsAvailable: 2);

        await _service.Received(1).GetUpcomingAsync("PSI", 2, null, request.Token);
    }
}
