using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Services;
using ImmichLoungeCompanion.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Services;

[TestClass]
public class ProfileScheduleServiceTests
{
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ProfileSchedule Selected(string start = "08:00", string end = "18:00", DayOfWeek day = DayOfWeek.Monday, string zone = "UTC") => new()
    {
        Id = "living-room",
        Name = "Living room",
        FallbackProfileId = "base-profile",
        Enabled = true,
        TimeZoneId = zone,
        Entries = [new() { ProfileId = "day-profile", Days = [(ScheduleDay)day], Start = start, End = end }]
    };

    [TestMethod]
    [DataRow("2026-10-05T07:59:00Z", "base-profile")]
    [DataRow("2026-10-05T08:00:00Z", "day-profile")]
    [DataRow("2026-10-05T17:59:00Z", "day-profile")]
    [DataRow("2026-10-05T18:00:00Z", "base-profile")]
    [DataRow("2026-10-06T09:00:00Z", "base-profile")]
    public async Task DaySlot_UsesInclusiveStartExclusiveEndAndChosenDay(string instant, string expected)
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        repository.GetAsync("day-profile").Returns(new Profile { Id = "day-profile" });
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse(instant)));
        Assert.AreEqual(expected, (await service.ResolveAsync(Selected()))?.Id);
    }

    [TestMethod]
    [DataRow("2026-10-11T21:59:00Z", "base-profile")]
    [DataRow("2026-10-11T22:00:00Z", "day-profile")]
    [DataRow("2026-10-12T05:59:00Z", "day-profile")]
    [DataRow("2026-10-12T06:00:00Z", "base-profile")]
    public async Task OvernightSlot_UsesStartDayAcrossSundayBoundary(string instant, string expected)
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        repository.GetAsync("day-profile").Returns(new Profile { Id = "day-profile" });
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse(instant)));
        Assert.AreEqual(expected, (await service.ResolveAsync(Selected("22:00", "06:00", DayOfWeek.Sunday)))?.Id);
    }

    [TestMethod]
    [DataRow("2026-01-05T07:30:00Z", "08:00", "09:00", DayOfWeek.Monday, "day-profile")]
    [DataRow("2026-07-06T06:30:00Z", "08:00", "09:00", DayOfWeek.Monday, "day-profile")]
    [DataRow("2026-10-25T00:30:00Z", "02:00", "03:00", DayOfWeek.Sunday, "day-profile")]
    [DataRow("2026-10-25T01:30:00Z", "02:00", "03:00", DayOfWeek.Sunday, "day-profile")]
    [DataRow("2026-03-29T01:30:00Z", "02:00", "03:00", DayOfWeek.Sunday, "base-profile")]
    public async Task ExplicitTimeZone_UsesLocalWallTimeAcrossDst(string instant, string start, string end, DayOfWeek day, string expected)
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        repository.GetAsync("day-profile").Returns(new Profile { Id = "day-profile" });
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse(instant)));
        Assert.AreEqual(expected, (await service.ResolveAsync(Selected(start, end, day, "Europe/Zurich")))?.Id);
    }

    [TestMethod]
    public async Task EqualTimes_Means24HoursFromChosenDay()
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        repository.GetAsync("day-profile").Returns(new Profile { Id = "day-profile" });
        var selected = Selected("08:00", "08:00");
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-06T07:59:00Z")));
        Assert.AreEqual("day-profile", (await service.ResolveAsync(selected))?.Id);
    }

    [TestMethod]
    public async Task FirstMatchingSlotWinsWithoutFollowingTargetSchedule()
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        var target = new Profile { Id = "day-profile" };
        target.Id = "day-profile";
        repository.GetAsync("day-profile").Returns(target);
        var selected = Selected();
        selected.Entries.Add(new() { ProfileId = "other-profile", Days = [ScheduleDay.Monday], Start = "00:00", End = "00:00" });
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-05T09:00:00Z")));
        Assert.AreSame(target, await service.ResolveAsync(selected));
        await repository.DidNotReceive().GetAsync("other-profile");
    }

    [TestMethod]
    public async Task DisabledDeletedOrInvalidScheduleFallsBackToSelectedProfile()
    {
        var repository = Substitute.For<IProfileRepository>();
        repository.GetAsync("base-profile").Returns(new Profile { Id = "base-profile" });
        var selected = Selected();
        var service = new ProfileScheduleService(repository, new FixedClock(DateTimeOffset.Parse("2026-10-05T09:00:00Z")));
        Assert.AreEqual("base-profile", (await service.ResolveAsync(selected))?.Id);
        selected.Enabled = false;
        Assert.AreEqual("base-profile", (await service.ResolveAsync(selected))?.Id);
        selected.Enabled = true;
        selected.TimeZoneId = "not-a-zone";
        Assert.IsNull(await service.ResolveAsync(selected));
    }

    [TestMethod]
    public void MalformedTimesDaysAndTargetIdsAreRejected()
    {
        var selected = Selected();
        selected.Entries[0].Start = "8:00";
        Assert.IsNotNull(ProfileScheduleService.Validate(selected));
        selected.Entries[0].Start = "08:00";
        selected.Entries[0].Days = [];
        Assert.IsNotNull(ProfileScheduleService.Validate(selected));
        selected.Entries[0].Days = [(ScheduleDay)8];
        Assert.IsNotNull(ProfileScheduleService.Validate(selected));
        selected.Entries[0].Days = [ScheduleDay.Monday];
        selected.Entries[0].ProfileId = "../settings";
        Assert.IsNotNull(ProfileScheduleService.Validate(selected));
    }
}
