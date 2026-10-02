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

    // Two independent, optional filters. Skipping the first to reach only the second
    // (Filter(minSeatsAvailable: 2)) isn't just a style choice - C# has no positional
    // syntax to "skip" an argument, so naming is the only way to reach a later optional
    // parameter without also committing to (and hardcoding) an earlier one's default.
    public static IEnumerable<StudySession> Filter(
        this IEnumerable<StudySession> sessions,
        string? course = null,
        int? minSeatsAvailable = null)
    {
        if (course is not null)
        {
            sessions = sessions.Where(session => session.Course.Contains(course, StringComparison.OrdinalIgnoreCase));
        }

        if (minSeatsAvailable is not null)
        {
            sessions = sessions.Where(session => session.SeatsAvailable >= minSeatsAvailable);
        }

        return sessions;
    }
}
