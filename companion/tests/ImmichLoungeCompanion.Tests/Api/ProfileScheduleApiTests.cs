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
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-05T09:00:00Z");
    }

    private static Profile Scheduled() => new()
    {
        Id = "living-room",
        Name = "Living room",
        Schedule = new()
        {
            Enabled = true,
            Entries = [new() { ProfileId = "day-profile", Days = [ScheduleDay.Monday], Start = "08:00", End = "18:00" }]
        }
    };

    [TestMethod]
    public async Task EditDocumentRemainsIntactWhilePlaybackResolvesTargetAndKeepsSelection()
    {
        using var factory = new TestWebApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedClock());
        }));
        using var client = configured.CreateClient();
        var target = new Profile { Id = "day-profile", Name = "Day", Display = new() { ZoomToFit = true } };
        Assert.AreEqual(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profiles", target)).StatusCode);
        Assert.AreEqual(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/profiles", Scheduled())).StatusCode);
        var edit = await client.GetFromJsonAsync<Profile>("/api/profiles/living-room");
        Assert.AreEqual("Living room", edit!.Name);
        Assert.IsTrue(edit.Schedule!.Enabled);
        Assert.IsFalse(edit.Display.ZoomToFit);
        var playback = await client.GetFromJsonAsync<JsonElement>("/api/profiles/living-room?resolveSchedule=true");
        Assert.AreEqual("living-room", playback.GetProperty("id").GetString());
        Assert.AreEqual("day-profile", playback.GetProperty("playbackProfileId").GetString());
        Assert.AreEqual("Day", playback.GetProperty("name").GetString());
        Assert.IsTrue(playback.GetProperty("display").GetProperty("zoomToFit").GetBoolean());
        Assert.IsTrue(playback.GetProperty("scheduleEnabled").GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, playback.GetProperty("schedule").ValueKind);
        edit.Schedule.Enabled = false;
        Assert.AreEqual(HttpStatusCode.OK, (await client.PutAsJsonAsync("/api/profiles/living-room", edit)).StatusCode);
        playback = await client.GetFromJsonAsync<JsonElement>("/api/profiles/living-room?resolveSchedule=true");
        Assert.AreEqual("living-room", playback.GetProperty("playbackProfileId").GetString());
        Assert.IsFalse(playback.GetProperty("scheduleEnabled").GetBoolean());
        var saved = await File.ReadAllTextAsync(Path.Combine(factory.DataDirectory, "profiles", "living-room.json"));
        Assert.IsFalse(saved.Contains("apiKey"));
        Assert.IsTrue(saved.Contains("schedule"));
    }

    [TestMethod]
    public async Task MissingAndSelfTargetsAreRejectedAndDeletedTargetFallsBack()
    {
        using var factory = new TestWebApplicationFactory();
        using var client = factory.CreateClient();
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profiles", Scheduled())).StatusCode);
        var self = Scheduled();
        self.Schedule!.Entries[0].ProfileId = self.Id;
        Assert.AreEqual(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profiles", self)).StatusCode);
        await client.PostAsJsonAsync("/api/profiles", new Profile { Id = "day-profile", Name = "Day" });
        await client.PostAsJsonAsync("/api/profiles", Scheduled());
        await client.DeleteAsync("/api/profiles/day-profile");
        var result = await client.GetFromJsonAsync<JsonElement>("/api/profiles/living-room?resolveSchedule=true");
        Assert.AreEqual("living-room", result.GetProperty("playbackProfileId").GetString());
    }
}
