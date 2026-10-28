using CoolApp.Controllers;
using CoolApp.Models;
using CoolApp.Services;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace CoolApp.Api.Tests.MSTest.Unit;

// The MSTest version of CoolApp.Api.Tests.XUnit/Unit/StudySessionsControllerTests.cs.
// NSubstitute doesn't care which test framework runs it, so the stubs and mocks look
// exactly the same.
[TestClass]
public class StudySessionsControllerTests
{
    private readonly IStudySessionService _service = Substitute.For<IStudySessionService>();
    private readonly StudySessionsController _controller;
    private readonly TestContext _testContext;

    public StudySessionsControllerTests(TestContext testContext)
    {
        _testContext = testContext;
        _controller = new StudySessionsController(_service);
    }

    private CancellationToken Cancellation => _testContext.CancellationToken;

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

    [TestMethod]
    public async Task GetById_ServiceFindsTheSession_ReturnsIt()
    {
        var session = Session(7);
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns(session);

        var result = await _controller.GetById(7, Cancellation);

        Assert.AreSame(session, result.Value);
    }

    [TestMethod]
    public async Task GetById_ServiceReturnsNull_Returns404()
    {
        _service.GetByIdAsync(7, Arg.Any<CancellationToken>()).Returns((StudySession?)null);

        var result = await _controller.GetById(7, Cancellation);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
    }

    [TestMethod]
    public async Task Create_ReturnsCreatedAtGetByIdWithTheNewId()
    {
        var created = Session(42);
        _service.CreateAsync(Arg.Any<StudySession>(), Arg.Any<CancellationToken>()).Returns(created);

        var result = await _controller.Create(Session(0), Cancellation);

        var createdAt = Assert.IsInstanceOfType<CreatedAtActionResult>(result.Result);
        Assert.AreEqual(nameof(StudySessionsController.GetById), createdAt.ActionName);
        Assert.AreEqual(42, createdAt.RouteValues!["id"]);
        Assert.AreSame(created, createdAt.Value);
    }

    [TestMethod]
    [DataRow(true, typeof(NoContentResult))]
    [DataRow(false, typeof(NotFoundResult))]
    public async Task RateSession_ServiceResult_MapsToStatusCode(bool rated, Type expected)
    {
        _service.RateAsync(7, 5, Arg.Any<CancellationToken>()).Returns(rated);

        var result = await _controller.RateSession(7, new RatingSubmission(5), Cancellation);

        Assert.IsInstanceOfType(result, expected);
    }

    [TestMethod]
    public async Task GetAll_PassesTheFiltersAndTheTokenToTheService()
    {
        using var request = new CancellationTokenSource();

        await _controller.GetAll(request.Token, course: "PSI", minSeatsAvailable: 2);

        await _service.Received(1).GetUpcomingAsync("PSI", 2, null, request.Token);
    }
}
