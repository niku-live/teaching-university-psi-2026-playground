using CoolApp.Models;

namespace CoolApp.Extensions;

public static class StudySessionExtensions
{
    // asOf defaults to "right now" but can be overridden - e.g. from a test that
    // needs a fixed, repeatable point in time instead of the real clock.
    public static IEnumerable<StudySession> UpcomingOnly(this IEnumerable<StudySession> sessions, DateTimeOffset? asOf = null)
    {
        var cutoff = asOf ?? DateTimeOffset.UtcNow;
        return sessions.Where(session => session.StartsAt > cutoff);
    }
}
