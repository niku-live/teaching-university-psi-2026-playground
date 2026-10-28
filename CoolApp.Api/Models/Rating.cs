namespace CoolApp.Models;

// A plain struct, not a record struct like StudySessionSummary: it deliberately has
// no built-in `==` (structs only get member-wise Equals by default, per this week's
// theory) - comparing two Ratings needs .Equals(), not ==.
//
// The constructor validates on the way in - but a real struct caveat is worth being
// honest about: C# always gives a struct an implicit public parameterless constructor
// (default(Rating), or new Rating[n]) producing Value = 0, bypassing this constructor
// entirely. No struct can fully prevent that; it's a language-level guarantee, not a
// bug here. In this codebase HostRating is only ever assigned from RatingSubmission's
// already-validated int (see [BindNever] on StudySession.HostRating, which keeps a
// client from setting it by just including it in a POST/PUT body directly), so the
// gap never actually opens in practice - but it's real, and worth knowing about before
// relying on "the constructor always runs" for any other struct.
public readonly struct Rating
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value < 1 || value > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Rating must be between 1 and 5.");
        }

        Value = value;
    }

    public override string ToString() => $"{Value}/5";
}
