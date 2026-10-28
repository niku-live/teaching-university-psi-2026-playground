using CoolApp.Models;

namespace CoolApp.Api.Tests.NUnit.Unit;

// The NUnit version of CoolApp.Api.Tests.XUnit/Unit/RatingTests.cs.
public class RatingTests
{
    [TestCase(1)]
    [TestCase(5)]
    public void Constructor_ValueInRange_IsKept(int value)
    {
        var rating = new Rating(value);

        Assert.That(rating.Value, Is.EqualTo(value));
    }

    [TestCase(0)]
    [TestCase(6)]
    [TestCase(-1)]
    public void Constructor_ValueOutOfRange_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rating(value));
    }
}
