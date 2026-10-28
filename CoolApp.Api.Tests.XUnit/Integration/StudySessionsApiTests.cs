using System.Net;
using System.Net.Http.Json;
using CoolApp.Models;

namespace CoolApp.Api.Tests.XUnit.Integration;

// Starts the real application (Program.cs) in memory and talks to it over HTTP. These
// test what the unit tests cannot: routing, model validation, JSON serialization, the
// Dependency Injection registrations - and now a real database (see StudySpotApiFactory).
//
// One application (and one database) is shared by all tests, because starting it for
// every test would be slow. So each test creates the sessions it needs instead of
// relying on what another test left behind.
public class StudySessionsApiTests(StudySpotApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static object ValidSession(string topic) => new
    {
        course = "Software Development I",
        topic,
        location = "MIF, room 401",
        startsAt = DateTimeOffset.UtcNow.AddDays(7),
        hostName = "Ieva",
        seatsAvailable = 4
    };

    [Fact]
    public async Task Create_ValidSession_Returns201AndCanBeReadBack()
    {
        var response = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Integration testing"), Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await _client.GetFromJsonAsync<StudySession>(response.Headers.Location, Cancellation);
        Assert.Equal("Integration testing", created!.Topic);
    }

    [Fact]
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

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/studysessions/999", Cancellation);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_ReturnsStatusAsText()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("JSON contract"), Cancellation);

        var json = await _client.GetStringAsync(created.Headers.Location, Cancellation);

        // Checks the JSON contract the frontend relies on: the enum is sent as its name
        // (JsonStringEnumConverter in Program.cs), not as a number.
        Assert.Contains("\"status\":\"Scheduled\"", json);
    }

    [Fact]
    public async Task Rate_ValueOutOfRange_Returns400()
    {
        var created = await _client.PostAsJsonAsync("/api/studysessions", ValidSession("Rate me"), Cancellation);

        var response = await _client.PutAsJsonAsync($"{created.Headers.Location}/rating", new { value = 9 }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
