using System.Net;
using System.Net.Http.Json;
using ImmichLoungeCompanion.Playlist;
using ImmichLoungeCompanion.Models;
using ImmichLoungeCompanion.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ImmichLoungeCompanion.Tests.Api;

[TestClass]
public class PlaylistApiTests : IDisposable
{
    private readonly TestWebApplicationFactory _factory = new();
    private readonly HttpClient _client;

    public PlaylistApiTests() => _client = _factory.CreateClient();
    public void Dispose()
    {
        // Invalidation shares the persistence gate and prevents queued writes from
        // recreating cache files while the factory deletes its temporary directory.
        _factory.Services.GetRequiredService<IPlaylistCache>().InvalidateAll();
        _factory.Dispose();
    }

    private async Task<IPlaylistCache> CreateCyclePlaylistAsync(bool shuffle = true)
    {
        var response = await _client.PostAsJsonAsync("/api/profiles", new Profile
        {
            Id = "cycle-playlist", Name = "Cycle playlist",
            Slideshow = new() { Shuffle = shuffle }
        });
        response.EnsureSuccessStatusCode();
        var cache = _factory.Services.GetRequiredService<IPlaylistCache>();
        cache.Set("cycle-playlist", new PlaylistCacheEntry(
            Enumerable.Range(0, 123).Select(i => new PlaylistEntry($"photo-{i}", "photo", null, null)).ToList(),
            DateTimeOffset.UtcNow, "v1"));
        return cache;
    }

    [TestMethod]
    public async Task ShuffleCycle_PagesCoverEveryAssetOnce_AndRefreshKeepsTheSameWindow()
    {
        var cache = await CreateCyclePlaylistAsync();
        var originalOrder = cache.Get("cycle-playlist")!.Assets.Select(a => a.Id).ToArray();
        var seen = new List<string>();
        var offset = 0;
        do
        {
            var url = $"/api/profiles/cycle-playlist/playlist?count=50&offset={offset}&playlistVersion=v1~12345";
            var page = (await _client.GetFromJsonAsync<PlaylistResponse>(url))!;
            var refreshed = (await _client.GetFromJsonAsync<PlaylistResponse>(url))!;
            Assert.AreEqual("v1~12345", page.PlaylistVersion);
            Assert.AreEqual(offset, page.Offset);
            CollectionAssert.AreEqual(page.Assets, refreshed.Assets);
            seen.AddRange(page.Assets.Select(a => a.Id));
            offset = page.NextOffset;
        } while (offset != 0);

        Assert.AreEqual(123, seen.Count);
        CollectionAssert.AreEquivalent(originalOrder, seen.ToArray());
        CollectionAssert.AreEqual(originalOrder, cache.Get("cycle-playlist")!.Assets.Select(a => a.Id).ToArray(),
            "A device's shuffle cycle must not mutate the shared playlist.");
        Assert.AreEqual("v1", cache.Get("cycle-playlist")!.PlaylistVersion);
    }

    [TestMethod]
    public async Task ShuffleCycle_NewSeedChangesOrder_WithoutChangingOtherDevices()
    {
        await CreateCyclePlaylistAsync();
        var first = (await _client.GetFromJsonAsync<PlaylistResponse>("/api/profiles/cycle-playlist/playlist?count=1000&playlistVersion=v1~12345"))!;
        var second = (await _client.GetFromJsonAsync<PlaylistResponse>("/api/profiles/cycle-playlist/playlist?count=1000&playlistVersion=v1~67890"))!;
        var resumedFirst = (await _client.GetFromJsonAsync<PlaylistResponse>("/api/profiles/cycle-playlist/playlist?count=1000&playlistVersion=v1~12345"))!;
        CollectionAssert.AreEquivalent(first.Assets, second.Assets);
        Assert.IsFalse(first.Assets.SequenceEqual(second.Assets));
        CollectionAssert.AreEqual(first.Assets, resumedFirst.Assets);
    }

    [TestMethod]
    public async Task ShuffleCycle_ContentVersionChangeResetsOffset_ButRetainsCycleSeed()
    {
        await CreateCyclePlaylistAsync();
        var page = (await _client.GetFromJsonAsync<PlaylistResponse>("/api/profiles/cycle-playlist/playlist?count=50&offset=50&playlistVersion=old~12345"))!;
        Assert.AreEqual(0, page.Offset);
        Assert.AreEqual("v1~12345", page.PlaylistVersion);
    }

    [TestMethod]
    public async Task ShuffleCycle_ShuffleDisabledKeepsChronologicalOrder()
    {
        var cache = await CreateCyclePlaylistAsync(shuffle: false);
        var page = (await _client.GetFromJsonAsync<PlaylistResponse>("/api/profiles/cycle-playlist/playlist?count=50&offset=50&playlistVersion=v1~12345"))!;
        Assert.AreEqual(50, page.Offset);
        Assert.AreEqual("v1", page.PlaylistVersion);
        CollectionAssert.AreEqual(cache.Get("cycle-playlist")!.Assets.Skip(50).Take(50).ToList(), page.Assets);
    }

    [TestMethod]
    public async Task GetPlaylist_ProfileNotFound_Returns404()
    {
        var response = await _client.GetAsync("/api/profiles/nope/playlist");
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task GetPlaylist_ColdCache_ReturnsBuildingTrue()
    {
        // Create a profile first
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "test-playlist",
            name = "Test",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = true, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        var cache = _factory.Services.GetRequiredService<IPlaylistCache>();
        try
        {
            var response = await _client.GetAsync("/api/profiles/test-playlist/playlist");
            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
            Assert.IsTrue(body!.Building);
            Assert.IsFalse(body.Cached);
            Assert.AreEqual(0, body.Assets.Count);
        }
        finally
        {
            // The response intentionally precedes the background build. Let the
            // build finish before teardown invalidates its cache and deletes data.
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (cache.IsBuilding("test-playlist"))
            {
                await Task.Delay(10, timeout.Token);
            }
        }
    }

    [TestMethod]
    public async Task GetPlaylist_WhenShuffleDisabled_PreservesCachedOrder()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "ordered-playlist",
            name = "Ordered",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("ordered-playlist", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null)
        ], DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/api/profiles/ordered-playlist/playlist?count=3");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.IsFalse(string.IsNullOrWhiteSpace(body.PlaylistVersion));
    }

    [TestMethod]
    public async Task GetPlaylist_WithOffset_ReturnsStableWindowAndNextOffset()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "paged-playlist",
            name = "Paged",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("paged-playlist", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null),
            new PlaylistEntry("d", "photo", null, null)
        ], DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/api/profiles/paged-playlist/playlist?count=2&offset=2");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "c", "d" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(2, body.Offset);
        Assert.AreEqual(0, body.NextOffset);
        Assert.AreEqual(4, body.TotalCount);
        Assert.IsFalse(string.IsNullOrWhiteSpace(body.PlaylistVersion));
    }

    [TestMethod]
    public async Task GetPlaylist_WithMismatchedPlaylistVersion_ResetsOffsetToZero()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "versioned-playlist",
            name = "Versioned",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("versioned-playlist", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null),
            new PlaylistEntry("d", "photo", null, null)
        ], DateTimeOffset.UtcNow, "v-current"));

        var response = await _client.GetAsync("/api/profiles/versioned-playlist/playlist?count=2&offset=2&playlistVersion=v-old");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "a", "b" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(0, body.Offset);
        Assert.AreEqual("v-current", body.PlaylistVersion);
    }

    [TestMethod]
    public async Task GetPlaylist_WithNegativeOffset_NormalizesToWrappedWindow()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "negative-offset",
            name = "Negative",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("negative-offset", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null),
            new PlaylistEntry("d", "photo", null, null)
        ], DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/api/profiles/negative-offset/playlist?count=2&offset=-1");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "d" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(3, body.Offset);
        Assert.AreEqual(0, body.NextOffset);
        Assert.AreEqual(4, body.TotalCount);
    }

    [TestMethod]
    public async Task GetPlaylist_WithOverflowOffset_WrapsUsingModulo()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "overflow-offset",
            name = "Overflow",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("overflow-offset", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null),
            new PlaylistEntry("d", "photo", null, null)
        ], DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/api/profiles/overflow-offset/playlist?count=2&offset=6");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "c", "d" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(2, body.Offset);
        Assert.AreEqual(0, body.NextOffset);
        Assert.AreEqual(4, body.TotalCount);
    }

    [TestMethod]
    public async Task GetPlaylist_WhenCountExceedsRemainingTail_ReturnsTailOnly()
    {
        await _client.PostAsJsonAsync("/api/profiles", new
        {
            id = "tail-window",
            name = "Tail",
            contentSources = Array.Empty<object>(),
            mediaTypes = new { photos = true, videos = false, videoAudio = false, livePhotos = false },
            slideshow = new { intervalSeconds = 10, shuffle = false, transitionEffect = "fade", refreshIntervalMinutes = 60 },
            display = new { overlayStyle = "none", overlayFields = Array.Empty<string>(), overlayBehavior = "manual", overlayFadeSeconds = 5, clockAlwaysVisible = false, clockFormat = "HH:mm", weatherUnit = "celsius" },
            imageQuality = "preview"
        });

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<IPlaylistCache>();
        cache.Set("tail-window", new PlaylistCacheEntry(
        [
            new PlaylistEntry("a", "photo", null, null),
            new PlaylistEntry("b", "photo", null, null),
            new PlaylistEntry("c", "photo", null, null),
            new PlaylistEntry("d", "photo", null, null)
        ], DateTimeOffset.UtcNow));

        var response = await _client.GetAsync("/api/profiles/tail-window/playlist?count=10&offset=1");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        CollectionAssert.AreEqual(new[] { "b", "c", "d" }, body!.Assets.Select(a => a.Id).ToArray());
        Assert.AreEqual(1, body.Offset);
        Assert.AreEqual(0, body.NextOffset);
        Assert.AreEqual(4, body.TotalCount);
    }
}
