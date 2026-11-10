using System.Text.Json.Serialization;

namespace CoolApp.Models;

// Sent and read as its name ("Scheduled"), not a number - wherever it is serialized, including
// a test's HttpClient reading a StudySessionDto, not only by the app's own JSON options.
[JsonConverter(typeof(JsonStringEnumConverter<SessionStatus>))]
public enum SessionStatus
{
    Scheduled,
    Full,
}
