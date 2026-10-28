using System.Net;
using System.Net.Http.Json;
using CoolApp.Models;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CoolApp.Api.Tests.MSTest.Integration;

// The MSTest version of CoolApp.Api.Tests.XUnit/Integration/StudySessionsApiTests.cs.
// xUnit's IClassFixture becomes static [ClassInitialize]/[ClassCleanup] methods: they run
// once for the whole class, so all tests share one running application, and each test
// still creates the sessions it needs.
[TestClass]
public class StudySessionsApiTests(TestContext testContext)
{
    private static WebApplicationFactory<Program> _factory = null!;
    private static HttpClient _client = null!;

    [ClassInitialize]
    public static void StartApplication(TestContext _)
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [ClassCleanup]
    public static void StopApplication()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    private CancellationToken Cancellation => testContext.CancellationToken;

    private static object ValidSession(string topic) => new
    {
        course = "Software Development I",
        topic,
        location = "MIF, room 401",
        startsAt = DateTimeOffset.UtcNow.AddDays(7),
        hostName = "Ieva",
        seatsAvailable = 4
    };

    [TestMethod]
    public async Task Create_ValidSession_Returns201AndCanBeReadBack()
    {
        var response = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Integration testing"), Cancellation);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var created = await _client.GetFromJsonAsync<StudySession>(response.Headers.Location, Cancellation);
        Assert.AreEqual("Integration testing", created!.Topic);
    }

    [TestMethod]
    public async Task Create_StartsInThePast_Returns400()
    {
        var session = new
        {
            course = "Software Development I",
            topic = "Too late",
            location = "MIF, room 401",
            startsAt = DateTimeOffset.UtcNow.AddDays(-1),
            hostName = "Ieva",
            seatsAvailable = 4
        };

        var response = await _client.PostAsJsonAsync("/api/studysessions", session, Cancellation);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/studysessions/999", Cancellation);

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetById_ReturnsStatusAsText()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("JSON contract"), Cancellation);

        var json = await _client.GetStringAsync(created.Headers.Location, Cancellation);

        Assert.Contains("\"status\":\"Scheduled\"", json);
    }

    [TestMethod]
    public async Task Rate_ValueOutOfRange_Returns400()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Rate me"), Cancellation);

        var response = await _client.PutAsJsonAsync($"{created.Headers.Location}/rating", new { value = 9 }, Cancellation);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
