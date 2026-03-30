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
    public void Dispose() => _factory.Dispose();

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

        var response = await _client.GetAsync("/api/profiles/test-playlist/playlist");
        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PlaylistResponse>();
        Assert.IsTrue(body!.Building);
        Assert.IsFalse(body.Cached);
        Assert.AreEqual(0, body.Assets.Count);
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
