using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Services;
namespace ImmichLoungeCompanion.Tests.Services;
[TestClass]
public class DateFilterValidationTests
{
    [TestMethod]
    [DataRow("2024-02-29", "2024-02-29", true)]
    [DataRow("2024-02-30", null, false)]
    [DataRow(null, null, false)]
    [DataRow("2025-01-01", "2024-01-01", false)]
    [DataRow(null, "2024-12-31", true)]
    [DataRow("2024-02-29T12:00:00Z", "2024-02-29", true)]
    [DataRow(null, "9999-12-31", true)]
    [DataRow("2024-02-29T12:00:00", null, false)]
    [DataRow("2024-02-29T12:00:00+02:00", null, true)]
    public void RangeValidation(string? from, string? to, bool valid)
    {
        var error = new ProfileValidator().Validate(new Profile { DateFilter = new() { Type = "range", From = from, To = to } });
        Assert.AreEqual(valid, error == null, error);
    }
    [TestMethod]
    [DataRow(1, "days", true)]
    [DataRow(100, "years", true)]
    [DataRow(0, "months", false)]
    [DataRow(101, "months", false)]
    [DataRow(1, "centuries", false)]
    public void RollingValidation(int amount, string unit, bool valid)
    {
        var error = new ProfileValidator().Validate(new Profile { DateFilter = new() { Type = "rolling", Amount = amount, Unit = unit } });
        Assert.AreEqual(valid, error == null, error);
    }
}
