using CoolApp.Models;

namespace CoolApp.Api.Tests.MSTest.Unit;

// The MSTest version of CoolApp.Api.Tests.XUnit/Unit/RatingTests.cs.
[TestClass]
public class RatingTests
{
    [TestMethod]
    [DataRow(1)]
    [DataRow(5)]
    public void Constructor_ValueInRange_IsKept(int value)
    {
        var rating = new Rating(value);

        Assert.AreEqual(value, rating.Value);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(6)]
    [DataRow(-1)]
    public void Constructor_ValueOutOfRange_Throws(int value)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new Rating(value));
    }
}
