using System.Net;
using System.Net.Http.Json;
using ImmichLoungeCompanion.Immich;
using ImmichLoungeCompanion.Immich.Dtos;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Storage;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace ImmichLoungeCompanion.Tests.Playlist;

[TestClass]
public sealed class PhotoSourceTests
{
    private static readonly ImmichSettings Settings = new() { ServerUrl = "http://immich", ApiKey = "test" };
    private static ImmichAsset Photo(string id) => new()
    {
        Id = id, Type = "IMAGE", ExifInfo = new() { Country = "Maldives", FileSizeInByte = 4096 }
    };

    [TestMethod]
    [DataRow("all")]
    [DataRow("selected")]
    public async Task ExplicitSelection_PromotesOnlyMatchingMemoriesWithoutDuplicates(string source)
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([Photo("regular"), Photo("memory")]);
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns(
            [new ImmichMemory { Assets = [Photo("memory"), Photo("outside")] }]);
        var profile = new Profile
        {
            PhotoSource = source, LocationFilter = new() { Country = "Maldives" },
            ContentSources = [new() { Type = "album", Id = "holiday" }, new() { Type = "memories" }],
            Memories = new() { Prioritize = true }, Slideshow = new() { Shuffle = false }
        };
        var collected = await new PlaylistAssetCollector(client).CollectAsync(profile, Settings);
        var entries = PlaylistEntryProjector.CreateEntries(collected, profile);
        CollectionAssert.AreEqual(new[] { "memory", "regular" }, entries.Select(entry => entry.Id).ToArray());
        Assert.IsTrue(entries[0].IsMemory);
        Assert.IsFalse(entries[1].IsMemory);
        await client.Received(1).SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(request =>
            request.Country == "Maldives" && (source == "all" ? request.AlbumIds == null : request.AlbumIds![0] == "holiday")));
    }

    [TestMethod]
    public async Task AllPhotos_WithNoMemoriesTodayStillShowsTheLibrary()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns([Photo("holiday")]);
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns([]);
        var collected = await new PlaylistAssetCollector(client).CollectAsync(new Profile
        {
            PhotoSource = "all", ContentSources = [new() { Type = "memories" }],
            Memories = new() { Prioritize = true }
        }, Settings);
        CollectionAssert.AreEqual(new[] { "holiday" }, collected.Keys.ToArray());
    }

    [TestMethod]
    public async Task AllPhotos_IgnoresSavedAlbumScopeWhenExcludingPeople()
    {
        var client = Substitute.For<IImmichClient>();
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().PersonIds == null ? [Photo("regular"), Photo("excluded")] : [Photo("excluded")]);
        var collected = await new PlaylistAssetCollector(client).CollectAsync(new Profile
        {
            PhotoSource = "all", ContentSources = [new() { Type = "album", Id = "inactive-album" }],
            ExcludedPeople = [new() { Type = "person", Id = "excluded-person" }]
        }, Settings);
        CollectionAssert.AreEqual(new[] { "regular" }, collected.Keys.ToArray());
        await client.DidNotReceive().SearchAssetsAllPagesAsync(Settings, Arg.Is<SearchAssetsRequest>(request => request.AlbumIds != null));
    }

    [TestMethod]
    public async Task MemoriesOnly_AppliesDateLocationExclusionsAndFileSize()
    {
        var client = Substitute.For<IImmichClient>();
        var abroad = Photo("abroad");
        abroad.ExifInfo!.Country = "Switzerland";
        var small = Photo("small");
        small.ExifInfo!.FileSizeInByte = 512;
        client.SearchAssetsAllPagesAsync(Settings, Arg.Any<SearchAssetsRequest>()).Returns(call =>
            call.Arg<SearchAssetsRequest>().PersonIds == null
                ? [Photo("match"), Photo("excluded"), small, abroad]
                : [Photo("excluded")]);
        client.GetMemoriesAsync(Settings, Arg.Any<DateOnly>()).Returns(
            [new ImmichMemory { Assets = [Photo("match"), Photo("old"), Photo("excluded"), small, abroad] }]);
        var collected = await new PlaylistAssetCollector(client).CollectAsync(new Profile
        {
            PhotoSource = "memories", ContentSources = [new() { Type = "memories" }],
            DateFilter = new() { Type = "range", From = "2026-01-01" },
            LocationFilter = new() { Country = "Maldives" }, Quality = new() { MinFileSizeKb = 1 },
            ExcludedPeople = [new() { Type = "person", Id = "excluded-person" }]
        }, Settings);
        CollectionAssert.AreEqual(new[] { "match" }, collected.Keys.ToArray());
        Assert.IsTrue(collected["match"].IsMemory);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task DailyRebuild_ResetsOrderAndVersionButSameDayRefreshPreservesOrder(bool nextDay)
    {
        var cache = Substitute.For<IPlaylistCache>();
        var profiles = Substitute.For<IProfileRepository>();
        var builder = Substitute.For<IPlaylistBuilder>();
        var profile = new Profile { Id = "daily", PhotoSource = "all", ContentSources = [new() { Type = "memories" }] };
        profiles.GetAsync(profile.Id).Returns(profile);
        var old = new List<PlaylistEntry> { new("a", "photo", null, null), new("b", "photo", null, null) };
        cache.Get(profile.Id).Returns(new PlaylistCacheEntry(old, DateTimeOffset.Parse("2026-10-10T10:00:00Z"), "old"));
        cache.TryStartBuilding(profile.Id).Returns(true);
        builder.BuildAsync(profile, Arg.Any<CancellationToken>()).Returns(old.AsEnumerable().Reverse().ToList());
        PlaylistCacheEntry? published = null;
        cache.TrySet(profile.Id, Arg.Any<PlaylistCacheEntry>(), Arg.Any<long>()).Returns(call =>
        {
            published = call.Arg<PlaylistCacheEntry>();
            return true;
        });
        using var worker = new PlaylistCacheWorker(builder, cache, profiles, NullLogger<PlaylistCacheWorker>.Instance,
            new FixedClock(DateTimeOffset.Parse(nextDay ? "2026-10-11T10:00:00Z" : "2026-10-10T11:00:00Z")));
        await worker.RebuildAsync(profile.Id);
        CollectionAssert.AreEqual(nextDay ? new[] { "b", "a" } : new[] { "a", "b" }, published!.Assets.Select(entry => entry.Id).ToArray());
        Assert.AreEqual(!nextDay, published.PlaylistVersion == "old");
    }

    [TestMethod]
    public async Task DailyVersion_ChangesEvenWhenThePhotosAndOrderAreIdentical()
    {
        using var dir = new TempDataDirectory();
        var cache = new PlaylistCache(dir.Path);
        var profiles = Substitute.For<IProfileRepository>();
        var builder = Substitute.For<IPlaylistBuilder>();
        var profile = new Profile { Id = "single", PhotoSource = "all", Memories = new() { IncludeBirthdays = true } };
        profiles.GetAsync(profile.Id).Returns(profile);
        List<PlaylistEntry> entries = [new("a", "photo", null, null)];
        cache.Set(profile.Id, new(entries, DateTimeOffset.Parse("2026-10-10T10:00:00Z"), "old"));
        builder.BuildAsync(profile, Arg.Any<CancellationToken>()).Returns(entries);
        using var worker = new PlaylistCacheWorker(builder, cache, profiles, NullLogger<PlaylistCacheWorker>.Instance,
            new FixedClock(DateTimeOffset.Parse("2026-10-11T10:00:00Z")));
        await worker.RebuildAsync(profile.Id);
        Assert.AreNotEqual("old", cache.Get(profile.Id)!.PlaylistVersion);
        cache.InvalidateAll();
    }

    [TestMethod]
    public async Task Api_RoundTripsExplicitSourceAndKeepsPriorityAcrossPagedShuffleCycles()
    {
        using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();
        var profile = new Profile
        {
            Id = "priority", Name = "Priority", PhotoSource = "all",
            Memories = new() { IncludeBirthdays = true, Prioritize = true, TimeZoneId = "Europe/Zurich" }
        };
        (await http.PostAsJsonAsync("/api/profiles", profile)).EnsureSuccessStatusCode();
        var reloaded = (await http.GetFromJsonAsync<Profile>("/api/profiles/priority"))!;
        Assert.AreEqual("all", reloaded.PhotoSource);
        Assert.IsTrue(reloaded.Memories.Prioritize);
        var cache = factory.Services.GetRequiredService<IPlaylistCache>();
        cache.Set("priority", new(
            [new("regular", "photo", null, null), new("memory", "photo", null, null, IsMemory: true)],
            DateTimeOffset.Parse("2026-10-10T22:01:00Z"), "daily"));
        var page = (await http.GetFromJsonAsync<PlaylistResponse>("/api/profiles/priority/playlist?count=1&playlistVersion=daily~123"))!;
        Assert.AreEqual("memory", page.Assets.Single().Id);
        Assert.AreEqual("2026-10-11", page.MemoryDay);
        var tail = (await http.GetFromJsonAsync<PlaylistResponse>("/api/profiles/priority/playlist?count=1&offset=1&playlistVersion=daily~123"))!;
        Assert.AreEqual("regular", tail.Assets.Single().Id);
    }

    [TestMethod]
    [DataRow("selected")]
    [DataRow("memories")]
    [DataRow("unknown")]
    public async Task InvalidExplicitSelection_IsRejectedBySaveAndPreview(string source)
    {
        using var factory = new TestWebApplicationFactory();
        using var http = factory.CreateClient();
        var profile = new Profile { Id = "invalid", Name = "Invalid", PhotoSource = source };
        Assert.AreEqual(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/profiles", profile)).StatusCode);
        Assert.AreEqual(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/profiles/preview-count", profile)).StatusCode);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
