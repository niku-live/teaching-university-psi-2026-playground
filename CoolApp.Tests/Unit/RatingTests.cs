using CoolApp.Models;

namespace CoolApp.Tests.Unit;

public class RatingTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void Constructor_ValueInRange_IsKept(int value)
    {
        var rating = new Rating(value);

        Assert.Equal(value, rating.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    [InlineData(-1)]
    public void Constructor_ValueOutOfRange_Throws(int value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Rating(value));
    }
}
