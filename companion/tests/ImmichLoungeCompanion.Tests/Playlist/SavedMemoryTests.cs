using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public class SavedMemoryTests
{
    [TestMethod]
    public async Task SavedOnlyProfileSettingsRoundTripThroughTheApi()
    {
        using var factory = new ImmichLoungeCompanion.Tests.Helpers.TestWebApplicationFactory();
        using var http = factory.CreateClient();
        var created = await http.PostAsJsonAsync("/api/profiles", new Profile
        {
            Id = "saved-profile", Name = "Saved memories", PhotoSource = "memories",
            Memories = new() { IncludeSaved = true, TimeZoneId = "UTC" }
        });
        Assert.AreEqual(HttpStatusCode.Created, created.StatusCode);
        var profile = await http.GetFromJsonAsync<Profile>("/api/profiles/saved-profile");
        Assert.IsTrue(profile!.Memories.IncludeSaved);
        Assert.IsFalse(profile.HasOnThisDaySource());
        Assert.IsFalse(profile.Memories.IncludeBirthdays);
        Assert.AreEqual("UTC", profile.Memories.TimeZoneId);
    }

    [TestMethod]
    public async Task SavedOnlyIncludesPastMemoriesWithoutBirthdayCaptionsOrDailyRequest()
    {
        var client = Mock("3.2.0");
        client.GetSavedMemoriesAsync(Arg.Any<ImmichSettings>()).Returns(
        [
            Birthday(), new() { Id = "unsaved", Type = "on_this_day", Assets = [new() { Id = "skip", Type = "IMAGE" }] }
        ]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "memories", Memories = new() { IncludeSaved = true }
        }, new());
        CollectionAssert.AreEqual(new[] { "photo" }, result.Keys.ToArray());
        Assert.IsNull(result["photo"].BirthdayCaption);
        await client.DidNotReceive().GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>());
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Arg.Any<ImmichSettings>(), Arg.Any<SearchAssetsRequest>());
    }

    [TestMethod]
    public async Task DailyAndSavedOverlapRetainsActiveBirthdayCaptionOnce()
    {
        var client = Mock("3.3.1");
        client.GetMemoriesAsync(Arg.Any<ImmichSettings>(), Arg.Any<DateOnly>()).Returns([Birthday()]);
        client.GetSavedMemoriesAsync(Arg.Any<ImmichSettings>()).Returns([Birthday()]);
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "memories", Memories = new() { IncludeSaved = true, IncludeBirthdays = true }
        }, new());
        Assert.HasCount(1, result);
        Assert.AreEqual("Emma's birthday", result["photo"].BirthdayCaption);
    }

    [TestMethod]
    public async Task OlderServersDoNotReceiveSavedMemoryQueries()
    {
        var client = Mock("3.1.0");
        var result = await new PlaylistAssetCollector(client).CollectAsync(new()
        {
            PhotoSource = "memories", Memories = new() { IncludeSaved = true }
        }, new());
        Assert.HasCount(0, result);
        await client.DidNotReceive().GetSavedMemoriesAsync(Arg.Any<ImmichSettings>());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task SavedMemoryPaginationIsCompleteAndRejectsRepeatedPages(bool repeat)
    {
        var handler = new Handler(repeat);
        using var http = new HttpClient(handler);
        var client = new ImmichClient(new Factory(http), NullLogger<ImmichClient>.Instance);
        var settings = new ImmichSettings { ServerUrl = "http://immich.example", ApiKey = "key" };
        if (repeat)
        {
            await Assert.ThrowsAsync<JsonException>(() => client.GetSavedMemoriesAsync(settings));
        }
        else
        {
            Assert.HasCount(101, await client.GetSavedMemoriesAsync(settings));
        }
        Assert.AreEqual(2, handler.Requests);
    }

    private static IImmichClient Mock(string version)
    {
        var client = Substitute.For<IImmichClient>();
        client.GetSearchApiCapabilitiesAsync(Arg.Any<ImmichSettings>(), Arg.Any<bool>()).Returns(new SearchApiCapabilities(version, true));
        return client;
    }
    private static ImmichMemory Birthday() => new() { Id = "birthday", Type = "birthday", IsSaved = true, Data = new() { PersonName = "Emma" }, Assets = [new() { Id = "photo", Type = "IMAGE" }] };
    private sealed class Factory(HttpClient http) : IHttpClientFactory { public HttpClient CreateClient(string name) => http; }
    private sealed class Handler(bool repeat) : HttpMessageHandler
    {
        public int Requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            Assert.AreEqual($"?isSaved=true&isUpcoming=false&page={Requests}&size=100", request.RequestUri!.Query);
            var ids = Requests == 1 || repeat ? Enumerable.Range(0, 100).Select(i => i.ToString()) : new[] { "last" };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(ids.Select(id => new { id, type = "on_this_day", isSaved = true, assets = Array.Empty<object>() })) });
        }
    }
}
