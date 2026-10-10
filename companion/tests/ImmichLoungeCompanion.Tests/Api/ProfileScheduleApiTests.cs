using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class ProfileScheduleApiTests
{
    private sealed class FixedClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.Parse("2026-10-05T09:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private static ProfileSchedule Schedule() => new()
    {
        Id = "living-room", Name = "Living room schedule", FallbackProfileId = "living-room",
        Entries = [new() { ProfileId = "day-profile", Days = [ScheduleDay.Monday], Start = "08:00", End = "18:00" }]
    };

    [TestMethod]
    public async Task SeparateScheduleCrud_ResolvesSlotsFallbackAndFixedProfilesWithoutModifyingProfiles()
    {
        using var factory = new TestWebApplicationFactory();
        var clock = new FixedClock();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
        }));
        using var client = configured.CreateClient();
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "living-room", Name = "Fallback" });
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "day-profile", Name = "Day", Display = new() { ZoomToFit = true } });
        var original = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "profiles", "living-room.json"));
        var created = await client.PostAsJsonAsync("/api/schedules", Schedule());
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var saved = await client.GetFromJsonAsync<ProfileSchedule>("/api/schedules/living-room");
        Assert.AreEqual("living-room", saved!.FallbackProfileId);
        var json = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "schedules", "living-room.json"));
        Assert.IsFalse(json.Contains("apiKey"));
        Assert.IsFalse(json.Contains("contentSources"));
        Assert.AreEqual(original, await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "profiles", "living-room.json")));
        var selections = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections");
        Assert.AreEqual(3, selections.GetArrayLength());
        Assert.IsTrue(selections.EnumerateArray().Any(item => item.GetProperty("id").GetString() == "schedule:living-room" && item.GetProperty("kind").GetString() == "schedule"));
        var active = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/schedule:living-room");
        Assert.AreEqual("schedule:living-room", active.GetProperty("id").GetString());
        Assert.AreEqual("Living room schedule", active.GetProperty("selectionName").GetString());
        Assert.AreEqual("day-profile", active.GetProperty("playbackProfileId").GetString());
        Assert.IsTrue(active.GetProperty("followingSchedule").GetBoolean());
        Assert.IsTrue(active.GetProperty("display").GetProperty("zoomToFit").GetBoolean());
        Assert.IsFalse(active.TryGetProperty("entries", out _));
        var fixedProfile = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/living-room");
        Assert.AreEqual("living-room", fixedProfile.GetProperty("playbackProfileId").GetString());
        Assert.IsFalse(fixedProfile.GetProperty("followingSchedule").GetBoolean());
        clock.Now = DateTimeOffset.Parse("2026-10-05T18:00:00Z");
        active = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/schedule:living-room");
        Assert.AreEqual("living-room", active.GetProperty("playbackProfileId").GetString());
        saved.Enabled = false;
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/schedules/living-room", saved)).StatusCode);
        clock.Now = DateTimeOffset.Parse("2026-10-05T09:00:00Z");
        active = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/schedule:living-room");
        Assert.AreEqual("living-room", active.GetProperty("playbackProfileId").GetString());
        Assert.IsTrue(active.GetProperty("followingSchedule").GetBoolean());
        Assert.AreEqual(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/schedules", Schedule())).StatusCode);
        Assert.AreEqual(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/schedules/living-room")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/api/playback/selections/schedule:living-room")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/profiles/living-room")).StatusCode);
    }

    [TestMethod]
    public async Task MissingTargetFallsBack_MissingFallbackReturnsUnavailableRatherThanDeletingSelection()
    {
        using var factory = new TestWebApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        }));
        using var client = configured.CreateClient();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/schedules", Schedule())).StatusCode);
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "living-room", Name = "Fallback" });
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "day-profile", Name = "Day" });
        await client.PostAsJsonAsync("/api/schedules", Schedule());
        await client.DeleteAsync("/api/profiles/day-profile");
        var active = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/schedule:living-room");
        Assert.AreEqual("living-room", active.GetProperty("playbackProfileId").GetString());
        var disabled = await client.GetFromJsonAsync<ProfileSchedule>("/api/schedules/living-room");
        disabled!.Enabled = false;
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/schedules/living-room", disabled)).StatusCode);
        await client.DeleteAsync("/api/profiles/living-room");
        Assert.AreEqual(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/playback/selections/schedule:living-room")).StatusCode);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/api/schedules/living-room")).StatusCode);
    }

    [TestMethod]
    public async Task InvalidScheduleAndPartialUpdateAreRejected_SelectionRoutesDoNotReserveProfileIds()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "living-room", Name = "Fallback" });
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "day-profile", Name = "Day" });
        var schedule = Schedule();
        schedule.Id = "../settings";
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/schedules", schedule)).StatusCode);
        schedule = Schedule();
        schedule.FallbackProfileId = "";
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/schedules", schedule)).StatusCode);
        await client.PostAsJsonAsync("/api/schedules", Schedule());
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/schedules/living-room", new { name = "Partial" })).StatusCode);
        Assert.AreEqual("Living room schedule", (await client.GetFromJsonAsync<ProfileSchedule>("/api/schedules/living-room"))!.Name);
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "selections", Name = "Reserved name test" });
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/playback/selections/selections");
        Assert.AreEqual("selections", profile.GetProperty("playbackProfileId").GetString());
    }
}
