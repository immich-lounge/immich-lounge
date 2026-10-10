using ImmichLoungeCompanion.Services;

namespace ImmichLoungeCompanion.Tests.Services;

[TestClass]
public sealed class ProfileIdGeneratorTests
{
    [TestMethod]
    [DataRow("Living Room", "living-room")]
    [DataRow("  Café & Family!  ", "cafe-family")]
    [DataRow("TV", "profile-tv")]
    [DataRow("日本語", "profile")]
    [DataRow("", "profile")]
    public void NamesGenerateApiCompatibleIds(string name, string expected)
        => Assert.AreEqual(expected, ProfileIdGenerator.FromName(name));

    [TestMethod]
    public void LongNamesRespectTheIdLimitWithoutTrailingHyphens()
    {
        var id = ProfileIdGenerator.FromName(new string('a', 63) + " and more");
        Assert.AreEqual(new string('a', 63), id);
    }
}
