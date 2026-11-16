using System.Net;
using System.Net.Http.Json;
using CoolApp.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace CoolApp.Api.Tests.MSTest.Integration;

// The MSTest version of CoolApp.Api.Tests.XUnit/Integration/StudySessionsApiTests.cs. The
// application and its database are started once for the whole run (see StudySpotApi),
// so each test creates the sessions it needs.
[TestClass]
public class StudySessionsApiTests(TestContext testContext)
{
    private static HttpClient _client = null!;

    [ClassInitialize]
    public static void CreateClient(TestContext _) => _client = StudySpotApi.Factory.CreateClient();

    [ClassCleanup]
    public static void DisposeClient() => _client.Dispose();

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
        var created = await _client.GetFromJsonAsync<StudySessionDto>(response.Headers.Location, Cancellation);
        Assert.AreEqual("Integration testing", created!.Topic);
    }

    // Over-posting: the request contract (CreateStudySessionDto) has no id or version, so a
    // client that sends them anyway can't choose them. Before the DTOs, this session was
    // stored with version 50.
    [TestMethod]
    public async Task Create_WithIdAndVersion_IgnoresThem()
    {
        var session = new
        {
            id = 999,
            version = 50,
            course = "Software Development I",
            topic = "Over-posting",
            location = "MIF, room 401",
            startsAt = DateTimeOffset.UtcNow.AddDays(7),
            hostName = "Ieva",
            seatsAvailable = 4
        };

        var response = await _client.PostAsJsonAsync("/api/studysessions", session, Cancellation);

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<StudySessionDto>(Cancellation);
        Assert.AreEqual(0, created!.Version);
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
    public async Task Update_WithTheVersionJustRead_Returns204()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Fresh edit"), Cancellation);
        var session = await _client.GetFromJsonAsync<StudySessionDto>(created.Headers.Location, Cancellation);

        var response = await _client.PutAsJsonAsync(created.Headers.Location, session! with { SeatsAvailable = 2 }, Cancellation);

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
    }

    [TestMethod]
    public async Task Update_WithAVersionSomeoneElseAlreadyChanged_Returns409Problem()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Two editors"), Cancellation);
        var first = await _client.GetFromJsonAsync<StudySessionDto>(created.Headers.Location, Cancellation);
        var second = await _client.GetFromJsonAsync<StudySessionDto>(created.Headers.Location, Cancellation);

        await _client.PutAsJsonAsync(created.Headers.Location, first! with { SeatsAvailable = 2 }, Cancellation);
        var response = await _client.PutAsJsonAsync(created.Headers.Location, second! with { Location = "Library, 2nd floor" }, Cancellation);

        Assert.AreEqual(HttpStatusCode.Conflict, response.StatusCode);
        Assert.AreEqual("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Cancellation);
        Assert.AreEqual("The study session was changed by someone else", problem!.Title);
    }

    [TestMethod]
    public async Task Rate_ValueOutOfRange_Returns400()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Rate me"), Cancellation);

        var response = await _client.PutAsJsonAsync($"{created.Headers.Location}/rating", new { value = 9 }, Cancellation);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
