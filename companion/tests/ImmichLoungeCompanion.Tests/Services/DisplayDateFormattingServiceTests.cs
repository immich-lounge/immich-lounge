using System;
using ImmichLoungeCompanion.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Services;

[TestClass]
public class DisplayDateFormattingServiceTests
{
    private readonly DisplayDateFormattingService _service = new();

    [TestMethod]
    public void FormatDate_UsesGermanDayPeriod_ForDefaultPattern()
    {
        var result = _service.FormatDate(new DateOnly(2026, 3, 24), "d MMMM yyyy", "de-DE");

        Assert.AreEqual("24. März 2026", result);
    }

    [TestMethod]
    public void FormatDate_PreservesLocalizedMonthNames()
    {
        var result = _service.FormatDate(new DateOnly(2026, 8, 1), "d MMMM yyyy", "fr-FR");

        Assert.AreEqual("1 août 2026", result);
    }

    [TestMethod]
    public void FormatDate_SupportsPolishMonthForms()
    {
        var result = _service.FormatDate(new DateOnly(2026, 3, 24), "d MMMM yyyy", "pl-PL");

        Assert.AreEqual("24 marca 2026", result);
    }
}
